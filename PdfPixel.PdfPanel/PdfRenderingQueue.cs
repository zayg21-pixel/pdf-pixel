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
    private readonly Func<Action, Task> _canvasDrawInvoker;
    private readonly Func<Action, Task> _thumbnailDrawInvoker;
    private readonly ConcurrentQueue<DrawingRequest> _updateQueue = new ConcurrentQueue<DrawingRequest>();
    private DrawingRequest _lastRequest;
    // volatile ensures the rendering worker's writes are immediately visible to the
    // enqueueing thread (typically the JS/main thread) that calls Cancel().
    private volatile CancellationTokenSource _currentRenderCts;

    public PdfRenderingQueue(ISkSurfaceFactory surfaceFactory, Func<Action, Task> canvasDrawInvoker = null, Func<Action, Task> thumbnailDrawInvoker = null)
    {
        _canvasDrawInvoker = canvasDrawInvoker ?? (action =>
        {
            action();
            return Task.CompletedTask;
        });
        _thumbnailDrawInvoker = thumbnailDrawInvoker ?? (action =>
        {
            action();
            return Task.CompletedTask;
        });

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

        try
        {
            _currentRenderCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CTS was disposed between the null-check and Cancel; cancellation is no longer needed.
        }
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
                    surface = await _surfaceFactory.GetDrawingSurfaceAsync(width, height);
                }

                if (request is PagesDrawingRequest pagesDrawingRequest)
                {
                    activePagesDrawingRequest = pagesDrawingRequest;
                    requiresRedraw = true;
                }
                else if (request is RefreshGraphicsDrawingRequest refreshGraphicsRequest)
                {
                    requiresRedraw = false;
                }
                else if (request is ResetDrawingRequest)
                {
                    surface = await _surfaceFactory.GetDrawingSurfaceAsync(width, height);
                    requiresRedraw = false;
                }

                if (requiresRedraw)
                {
                    var cts = new CancellationTokenSource();
                    _currentRenderCts = cts;
                    requiresRedraw = !await ProcessPagesDrawing(surface, activePagesDrawingRequest, previousPagesDrawingRequest, cts.Token).ConfigureAwait(false);
                    _currentRenderCts = null;
                    cts.Dispose();

                    previousPagesDrawingRequest = activePagesDrawingRequest;
                }
                else if (surface != null && activePagesDrawingRequest != null)
                {
                    await activePagesDrawingRequest.RenderTarget.RenderAsync(surface, request);
                }
            }
            catch (OperationCanceledException)
            {
                // A new request was enqueued while processing the current one; loop back to process the new request.
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                _currentRenderCts?.Dispose();
                _currentRenderCts = null;
#if DEBUG // TODO: inject logger
                throw;
#endif
                Console.WriteLine($"Error processing drawing request: {ex}");

            }
        }
    }

    private async Task<bool> ProcessPagesDrawing(
        SKSurface surface,
        PagesDrawingRequest request,
        PagesDrawingRequest previousRequest,
        CancellationToken cancellationToken)
    {
        SKCanvas canvas = null;
        await _canvasDrawInvoker.Invoke(() =>
        {
            canvas = surface.Canvas;
            canvas.ClipRect(new SKRect(0, 0, (float)request.CanvasSize.Width, (float)request.CanvasSize.Height));
        }).ConfigureAwait(false);

        HashSet<int> pagesWithThumbnailsDrawn = new HashSet<int>();
        HashSet<int> pagesWithContentDrawn = new HashSet<int>();

        await _canvasDrawInvoker.Invoke(() =>
        {
            var surfaceSnapshot = previousRequest == null ? null : surface.Snapshot();
            DrawBackgroundAndShadows(canvas, request);
            DrawExistingThumbnails(canvas, request, pagesWithThumbnailsDrawn);
            RenderSurfaceSnapshot(canvas, surfaceSnapshot, request, previousRequest);
            surfaceSnapshot?.Dispose();
        });

        await request.RenderTarget.RenderAsync(surface, request).ConfigureAwait(false);

        if (!_updateQueue.IsEmpty)
        {
            return false;
        }

        var visiblePages = request.VisiblePages.Select(x => x.PageNumber);

        var thumbnailSurface = request.MaxThumbnailSize > 0
            ? await _surfaceFactory.CreateThumbnailSurfaceAsync(request.MaxThumbnailSize, request.MaxThumbnailSize)
            : null;

        bool allDrawn = await RenderThumbnailsAndContent(
            surface,
            request,
            previousRequest,
            visiblePages,
            thumbnailSurface,
            pagesWithThumbnailsDrawn,
            pagesWithContentDrawn).ConfigureAwait(false);

        if (!_updateQueue.IsEmpty || pagesWithContentDrawn.Count == visiblePages.Count())
        {
            return allDrawn;
        }

        allDrawn = await RenderRemainingContent(
            surface,
            request,
            visiblePages,
            pagesWithContentDrawn,
            cancellationToken).ConfigureAwait(false);

        return allDrawn;
    }

    private void DrawBackgroundAndShadows(SKCanvas canvas, PagesDrawingRequest request)
    {
        canvas.Clear(request.BackgroundColor);

        foreach (var page in request.VisiblePages)
        {
            canvas.DrawPageFromRequest(page.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Shadow);
        }
    }

    private IEnumerable<int> GetExtendedVisiblePages(
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

    private async Task<bool> RenderThumbnailsAndContent(
        SKSurface surface,
        PagesDrawingRequest request,
        PagesDrawingRequest previousRequest,
        IEnumerable<int> visiblePages,
        SKSurface thumbnailSurface,
        HashSet<int> pagesWithThumbnailsDrawn,
        HashSet<int> pagesWithContentDrawn)
    {
        bool allDrawn = true;

        var extendedVisiblePages = GetExtendedVisiblePages(request, previousRequest, visiblePages);

        await foreach (var picture in request.Pages.UpdateCacheWithThumbnails(extendedVisiblePages, request.Scale, thumbnailSurface, _thumbnailDrawInvoker, request.ActiveAnnotation, request.ActiveAnnotationState))
        {
            if (!visiblePages.Contains(picture.PageNumber))
            {
                continue;
            }

            bool contentComplete = picture.Picture != null && (!picture.HasAnnotations || picture.AnnotationPicture != null);

            if (picture.Picture == null)
            {
                if (!pagesWithThumbnailsDrawn.Contains(picture.PageNumber))
                {
                    await _canvasDrawInvoker.Invoke(() =>
                    {
                        surface.Canvas.DrawPageFromRequest(picture.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Thumbnail);
                    }).ConfigureAwait(false);
                    pagesWithThumbnailsDrawn.Add(picture.PageNumber);
                }
            }
            else if (contentComplete)
            {
                await _canvasDrawInvoker.Invoke(() =>
                {
                    surface.Canvas.DrawPageFromRequest(picture.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Content);
                }).ConfigureAwait(false);
                pagesWithContentDrawn.Add(picture.PageNumber);
            }

            await request.RenderTarget.RenderAsync(surface, request).ConfigureAwait(false);

            if (!_updateQueue.IsEmpty)
            {
                allDrawn = false;
                break;
            }
        }

        return allDrawn;
    }

    private async Task<bool> RenderRemainingContent(
        SKSurface surface,
        PagesDrawingRequest request,
        IEnumerable<int> visiblePages,
        HashSet<int> pagesWithContentDrawn,
        CancellationToken cancellationToken)
    {
        bool allDrawn = true;

        try
        {
            foreach (var picture in request.Pages.GeneratePicturesForCachedPages(cancellationToken))
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
                    await _canvasDrawInvoker.Invoke(() =>
                    {
                        surface.Canvas.DrawPageFromRequest(picture.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Content);
                    }).ConfigureAwait(false);
                    pagesWithContentDrawn.Add(picture.PageNumber);
                    await request.RenderTarget.RenderAsync(surface, request).ConfigureAwait(false);
                }

                if (!_updateQueue.IsEmpty)
                {
                    allDrawn = false;
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // A new request was enqueued; stop rendering and signal that not everything was drawn.
            allDrawn = false;
        }

        return allDrawn;
    }

    private void DrawExistingThumbnails(
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

    private void RenderSurfaceSnapshot(
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

            canvas.Save();

            var lastPage = previousRequest.VisiblePages.FirstOrDefault(x => x.PageNumber == page.PageNumber);
            var sourceRect = lastPage.GetScaledBounds(previousRequest.Scale);
            var destRect = page.GetScaledBounds(request.Scale);

            if (request.PageCornerRadius > 0)
            {
                using var clipPath = new SKPath();
                clipPath.AddRoundRect(destRect, request.PageCornerRadius * request.Scale, request.PageCornerRadius * request.Scale);
                canvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias: true);
            }

            canvas.DrawImage(surfaceSnapshot, sourceRect, destRect);

            canvas.Restore();
        }
    }

    public void Dispose()
    {
        _currentRenderCts?.Cancel();
        _semaphore.Dispose();
    }
}
