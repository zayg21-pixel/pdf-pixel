using PdfRender.Canvas.Requests;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PdfRender.Canvas;

public interface ICanvasRenderTarget
{
    Task RenderAsync(SKSurface surface);
}

public interface ICanvasRenderTargetFactory
{
    ICanvasRenderTarget GetRenderTarget(PdfViewerCanvas renderCanvas);
}

public sealed class PdfRenderingQueue : IDisposable
{
    private readonly ISkSurfaceFactory _surfaceFactory;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
    private readonly ConcurrentQueue<DrawingRequest> _updateQueue = new ConcurrentQueue<DrawingRequest>();
    private DrawingRequest _lastRequest;

    public PdfRenderingQueue(ISkSurfaceFactory surfaceFactory)
    {
        _surfaceFactory = surfaceFactory;
        StartReadFromQueue();
    }

    public Action<string> OnLog;

    internal void EnqueueDrawingRequest(DrawingRequest request)
    {
        if (_lastRequest != null && _lastRequest.Equals(request))
        {
            return;
        }

        _updateQueue.Enqueue(request);
        _semaphore.Release();
        _lastRequest = request;
    }

    private async void StartReadFromQueue()
    {
        SKSurface surface = null;
        PagesDrawingRequest activePagesDrawingRequest = null;
        PagesDrawingRequest previousPagesDrawingRequest = null;
        bool pagesUpdated = false;

        while (true)
        {
            try
            {
                try
                {
                    await _semaphore.WaitAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (!_updateQueue.TryDequeue(out var request))
                {
                    break;
                }

                if (!_updateQueue.IsEmpty)
                {
                    if (request is PagesDrawingRequest skippedPagesDrawingRequest)
                    {
                        activePagesDrawingRequest = skippedPagesDrawingRequest;
                        pagesUpdated = true;
                    }

                    continue;
                }

                var width = (int)request.CanvasSize.Width;
                var height = (int)request.CanvasSize.Height;

                if (surface == null || surface.Canvas.DeviceClipBounds.Width != width || surface.Canvas.DeviceClipBounds.Height != height)
                {
                    var newSurface = CreateSurface(surface, width, height);

                    surface?.Dispose();
                    surface = newSurface;
                }

                if (request is PagesDrawingRequest pagesDrawingRequest)
                {
                    activePagesDrawingRequest = pagesDrawingRequest;
                    pagesUpdated = true;
                }
                else if (request is ResetDrawingRequest)
                {
                    surface?.Dispose();
                    surface = CreateSurface(null, width, height);
                    pagesUpdated = false;
                }

                if (pagesUpdated)
                {
                    pagesUpdated = !await ProcessPagesDrawing(surface, activePagesDrawingRequest, previousPagesDrawingRequest, _updateQueue).ConfigureAwait(false);
                    previousPagesDrawingRequest = activePagesDrawingRequest;
                }
                else if (surface != null && activePagesDrawingRequest != null)
                {
                    await activePagesDrawingRequest.RenderTarget.RenderAsync(surface);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Error in PdfRenderingQueue: {ex}");
#if DEBUG
                throw;
#endif
            }
        }

        surface?.Dispose();
    }

    private async Task<bool> ProcessPagesDrawing(
        SKSurface surface,
        PagesDrawingRequest request,
        PagesDrawingRequest lastRequest,
        ConcurrentQueue<DrawingRequest> updateQueue)
    {
        var canvas = surface.Canvas;
        canvas.ClipRect(new SKRect(0, 0, (float)request.CanvasSize.Width, (float)request.CanvasSize.Height));

        if (CanDrawCached(request))
        {
            DrawCached(surface, request, lastRequest);
            await request.RenderTarget.RenderAsync(surface).ConfigureAwait(false);
        }
        else
        {
            canvas.Clear(request.BackgroundColor);

            foreach (var page in request.VisiblePages)
            {
                canvas.DrawPageFromRequest(page.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Shadow);
            }

            await request.RenderTarget.RenderAsync(surface).ConfigureAwait(false);
        }

        if (!updateQueue.IsEmpty)
        {
            return false;
        }

        var visiblePages = request.VisiblePages.Select(x => x.PageNumber);
        var extendedVisiblePages = visiblePages;

        if (lastRequest != null && lastRequest.Scale == request.Scale)
        {
            if (lastRequest.Offset.Y <= request.Offset.Y && extendedVisiblePages.Any() && extendedVisiblePages.Max() != request.Pages.Count)
            {
                // Pre-cache next page as user scrolls down or zooms only
                extendedVisiblePages = extendedVisiblePages.Append(extendedVisiblePages.Max() + 1);
            }
            else if (lastRequest.Offset.Y > request.Offset.Y && extendedVisiblePages.Any() && extendedVisiblePages.Min() != 1)
            {
                // Pre-cache previous page as user scrolls up
                extendedVisiblePages = extendedVisiblePages.OrderByDescending(x => x).Append(extendedVisiblePages.Min() - 1);
            }
        }

        bool allDrawn = true;
        HashSet<int> drawnPages = new HashSet<int>();

        foreach (var picture in request.Pages.UpdateCacheAndGetPictures(extendedVisiblePages, request.Scale, request.MaxThumbnailSize))
        {
            if (!visiblePages.Contains(picture.PageNumber))
            {
                continue;
            }

            if (picture.Picture == null)
            {
                // TODO: [HIGH] move to separate common method with caching check
                var page = request.VisiblePages.First(x => x.PageNumber == picture.PageNumber);
                var destRect = page.GetScaledBounds(request.Scale);
                lock (picture.DisposeLocker)
                {
                    if (picture.IsDisposed)
                    {
                        continue;
                    }

                    var thumbnail = picture.Thumbnail;

                    if (thumbnail != null)
                    {
                        var thumbnailRect = SKRect.Create(0, 0, thumbnail.Width, thumbnail.Height);

                        var samplingOption = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
                        canvas.DrawImage(thumbnail, thumbnailRect, destRect, samplingOption);
                    }
                }
            }
            else
            {
                drawnPages.Add(picture.PageNumber);
                canvas.DrawPageFromRequest(picture.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Content);
            }

            await request.RenderTarget.RenderAsync(surface).ConfigureAwait(false);

            if (!updateQueue.IsEmpty)
            {
                allDrawn = false;
                break;
            }
        }

        if (!updateQueue.IsEmpty || drawnPages.Count == extendedVisiblePages.Count())
        {
            return allDrawn;
        }

        foreach (var picture in request.Pages.UpdateCacheAndGetPictures(extendedVisiblePages, request.Scale, request.MaxThumbnailSize))
        {
            if (drawnPages.Contains(picture.PageNumber))
            {
                continue;
            }

            if (!visiblePages.Contains(picture.PageNumber))
            {
                continue;
            }

            if (picture.Picture != null)
            {
                canvas.DrawPageFromRequest(picture.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Content);
                await request.RenderTarget.RenderAsync(surface).ConfigureAwait(false);
            }

            if (!updateQueue.IsEmpty)
            {
                allDrawn = false;
                break;
            }
        }

        return allDrawn;
    }

    private static bool CanDrawCachedPage(VisiblePageInfo page, PagesDrawingRequest request, out CachedSkPicture cached)
    {
        if (!request.Pages.TryGetPictureFromCache(page.PageNumber, out cached))
        {
            return false;
        }

        return true;
    }

    private static bool CanDrawCached(PagesDrawingRequest request)
    {
        bool shouldDraw = false;

        foreach (var page in request.VisiblePages)
        {
            if (CanDrawCachedPage(page, request, out _))
            {
                shouldDraw = true;
            }
        }

        return shouldDraw;
    }

    private static void DrawCached(SKSurface surface, PagesDrawingRequest request, PagesDrawingRequest lastRequest)
    {
        var canvas = surface.Canvas;
        using var surfaceImage = surface.Snapshot();

        canvas.Clear(request.BackgroundColor);

        foreach (var page in request.VisiblePages)
        {
            canvas.DrawPageFromRequest(page.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Shadow);
        }

        foreach (var page in request.VisiblePages)
        {
            if (!CanDrawCachedPage(page, request, out var cached))
            {
                continue;
            }

            if (cached.Thumbnail == null)
            {
                canvas.DrawPageFromRequest(page.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Content);
                continue;
            }

            var destRect = page.GetScaledBounds(request.Scale);

            lock (cached.DisposeLocker)
            {
                if (cached.IsDisposed)
                {
                    continue;
                }

                var thumbnail = cached.Thumbnail;

                if (thumbnail != null)
                {
                    var thumbnailRect = SKRect.Create(0, 0, thumbnail.Width, thumbnail.Height);

                    var samplingOption = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
                    canvas.DrawImage(thumbnail, thumbnailRect, destRect, samplingOption);
                }
            }

            if (lastRequest == null)
            {
                continue;
            }

            if (!lastRequest.VisiblePages.Any(x => x.PageNumber == page.PageNumber))
            {
                continue;
            }

            var lastPage = lastRequest.VisiblePages.FirstOrDefault(x => x.PageNumber == page.PageNumber);

            var sourceRect = lastPage.GetScaledBounds(lastRequest.Scale);

            canvas.DrawImage(surfaceImage, sourceRect, destRect);
        }
    }

    private SKSurface CreateSurface(SKSurface source, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

        var result = _surfaceFactory.GetSurface(info);

        if (source != null)
        {
            result.Canvas.DrawSurface(source, SKPoint.Empty);
        }

        return result;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
