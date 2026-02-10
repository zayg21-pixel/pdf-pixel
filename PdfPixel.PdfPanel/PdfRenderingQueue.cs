using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel;

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
        bool requiresRedraw = false;

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
                        requiresRedraw = true;
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
                    requiresRedraw = true;
                }
                else if (request is ResetDrawingRequest)
                {
                    surface?.Dispose();
                    surface = CreateSurface(null, width, height);
                    requiresRedraw = false;
                }

                if (requiresRedraw)
                {
                    requiresRedraw = !await ProcessPagesDrawing(surface, activePagesDrawingRequest, previousPagesDrawingRequest, _updateQueue).ConfigureAwait(false);
                    previousPagesDrawingRequest = activePagesDrawingRequest;
                }
                else if (surface != null && activePagesDrawingRequest != null)
                {
                    await activePagesDrawingRequest.RenderTarget.RenderAsync(surface);
                }
            }
            catch (Exception ex)
            {
#if DEBUG // TODO: inject logger
                throw;
#endif
            }
        }

        surface?.Dispose();
    }

    private async Task<bool> ProcessPagesDrawing(
        SKSurface surface,
        PagesDrawingRequest request,
        PagesDrawingRequest previousRequest,
        ConcurrentQueue<DrawingRequest> updateQueue)
    {
        var canvas = surface.Canvas;
        canvas.ClipRect(new SKRect(0, 0, (float)request.CanvasSize.Width, (float)request.CanvasSize.Height));

        HashSet<int> pagesWithThumbnailsDrawn = new HashSet<int>();
        HashSet<int> pagesWithContentDrawn = new HashSet<int>();

        var surfaceSnapshot = TakeSnapshot(surface, previousRequest);
        DrawBackgroundAndShadows(canvas, request);
        DrawExistingThumbnails(canvas, request, pagesWithThumbnailsDrawn);
        RenderSurfaceSnapshot(canvas, surfaceSnapshot, request, previousRequest);
        surfaceSnapshot?.Dispose();

        await request.RenderTarget.RenderAsync(surface).ConfigureAwait(false);

        if (!updateQueue.IsEmpty)
        {
            return false;
        }

        var visiblePages = request.VisiblePages.Select(x => x.PageNumber);

        bool allDrawn = await RenderThumbnailsAndContent(
            surface,
            request,
            previousRequest,
            visiblePages,
            pagesWithThumbnailsDrawn,
            pagesWithContentDrawn,
            updateQueue).ConfigureAwait(false);

        if (!updateQueue.IsEmpty || pagesWithContentDrawn.Count == visiblePages.Count())
        {
            return allDrawn;
        }

        allDrawn = await RenderRemainingContent(
            surface,
            request,
            visiblePages,
            pagesWithContentDrawn,
            updateQueue).ConfigureAwait(false);

        return allDrawn;
    }

    private static void DrawBackgroundAndShadows(SKCanvas canvas, PagesDrawingRequest request)
    {
        canvas.Clear(request.BackgroundColor);

        foreach (var page in request.VisiblePages)
        {
            canvas.DrawPageFromRequest(page.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Shadow);
        }
    }

    private static IEnumerable<int> GetExtendedVisiblePages(
        PagesDrawingRequest request,
        PagesDrawingRequest previousRequest,
        IEnumerable<int> visiblePages)
    {
        var extendedVisiblePages = visiblePages;

        if (previousRequest != null && previousRequest.Scale == request.Scale)
        {
            if (previousRequest.Offset.Y <= request.Offset.Y && extendedVisiblePages.Any() && extendedVisiblePages.Max() != request.Pages.Count)
            {
                extendedVisiblePages = extendedVisiblePages.Append(extendedVisiblePages.Max() + 1);
            }
            else if (previousRequest.Offset.Y > request.Offset.Y && extendedVisiblePages.Any() && extendedVisiblePages.Min() != 1)
            {
                extendedVisiblePages = extendedVisiblePages.OrderByDescending(x => x).Append(extendedVisiblePages.Min() - 1);
            }
        }

        return extendedVisiblePages;
    }

    private static async Task<bool> RenderThumbnailsAndContent(
        SKSurface surface,
        PagesDrawingRequest request,
        PagesDrawingRequest previousRequest,
        IEnumerable<int> visiblePages,
        HashSet<int> pagesWithThumbnailsDrawn,
        HashSet<int> pagesWithContentDrawn,
        ConcurrentQueue<DrawingRequest> updateQueue)
    {
        var canvas = surface.Canvas;
        bool allDrawn = true;

        var extendedVisiblePages = GetExtendedVisiblePages(request, previousRequest, visiblePages);

        foreach (var picture in request.Pages.UpdateCacheWithThumbnails(extendedVisiblePages, request.Scale, request.MaxThumbnailSize))
        {
            if (!visiblePages.Contains(picture.PageNumber))
            {
                continue;
            }

            if (picture.Picture == null)
            {
                if (!pagesWithThumbnailsDrawn.Contains(picture.PageNumber))
                {
                    canvas.DrawPageFromRequest(picture.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Thumbnail);
                    pagesWithThumbnailsDrawn.Add(picture.PageNumber);
                }
            }
            else
            {
                canvas.DrawPageFromRequest(picture.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Content);
                pagesWithContentDrawn.Add(picture.PageNumber);
            }

            await request.RenderTarget.RenderAsync(surface).ConfigureAwait(false);

            if (!updateQueue.IsEmpty)
            {
                allDrawn = false;
                break;
            }
        }

        return allDrawn;
    }

    private static async Task<bool> RenderRemainingContent(
        SKSurface surface,
        PagesDrawingRequest request,
        IEnumerable<int> visiblePages,
        HashSet<int> pagesWithContentDrawn,
        ConcurrentQueue<DrawingRequest> updateQueue)
    {
        var canvas = surface.Canvas;
        bool allDrawn = true;

        foreach (var picture in request.Pages.GeneratePicturesForCachedPages())
        {
            if (pagesWithContentDrawn.Contains(picture.PageNumber))
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
                pagesWithContentDrawn.Add(picture.PageNumber);
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

    private static SKImage TakeSnapshot(SKSurface surface, PagesDrawingRequest previousRequest)
    {
        if (previousRequest == null)
        {
            return null;
        }

        return surface.Snapshot();
    }

    private static void DrawExistingThumbnails(
        SKCanvas canvas,
        PagesDrawingRequest request,
        HashSet<int> pagesWithThumbnailsDrawn)
    {
        foreach (var page in request.VisiblePages)
        {
            if (!request.Pages.TryGetPictureFromCache(page.PageNumber, out var cached))
            {
                continue;
            }

            if (cached.Thumbnail == null)
            {
                continue;
            }

            canvas.DrawPageFromRequest(page.PageNumber, request, PageDrawFlags.Thumbnail);
            pagesWithThumbnailsDrawn.Add(page.PageNumber);
        }
    }

    private static void RenderSurfaceSnapshot(
        SKCanvas canvas,
        SKImage surfaceSnapshot,
        PagesDrawingRequest request,
        PagesDrawingRequest previousRequest)
    {
        if (surfaceSnapshot == null)
        {
            return;
        }

        foreach (var page in request.VisiblePages)
        {
            if (!previousRequest.VisiblePages.Any(x => x.PageNumber == page.PageNumber))
            {
                continue;
            }

            var lastPage = previousRequest.VisiblePages.FirstOrDefault(x => x.PageNumber == page.PageNumber);
            var sourceRect = lastPage.GetScaledBounds(previousRequest.Scale);
            var destRect = page.GetScaledBounds(request.Scale);

            canvas.DrawImage(surfaceSnapshot, sourceRect, destRect);
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
