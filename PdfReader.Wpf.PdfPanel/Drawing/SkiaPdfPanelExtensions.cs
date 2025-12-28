using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PdfReader.Wpf.PdfPanel.Drawing
{
    /// <summary>
    /// Extensions for drawing on <see cref="SkiaPdfPanel"/>.
    /// </summary>
    internal static class SkiaPdfPanelExtensions
    {
        public static async Task<bool> ProcessPagesDrawing(
            this SkiaPdfPanel panel,
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
                await DrawOnWritableBitmapAsync(panel, surface, request).ConfigureAwait(false);

                if (!updateQueue.IsEmpty)
                {
                    return false;
                }
            }
            else
            {
                canvas.Clear(request.BackgroundColor);

                foreach (var page in request.VisiblePages)
                {
                    canvas.DrawPageFromRequest(page.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Shadow);
                }
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
                    var destRect = page.GetSkScaledBounds(request.Scale);
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

                await DrawOnWritableBitmapAsync(panel, surface, request).ConfigureAwait(false);

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
                    await DrawOnWritableBitmapAsync(panel, surface, request).ConfigureAwait(false);
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

        public static bool CanDrawCached(PagesDrawingRequest request)
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

                var destRect = page.GetSkScaledBounds(request.Scale);

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

                var sourceRect = lastPage.GetSkScaledBounds(lastRequest.Scale);

                canvas.DrawImage(surfaceImage, sourceRect, destRect);
            }
        }

        public static async Task DrawOnWritableBitmapAsync(this SkiaPdfPanel panel, SKSurface surface, PagesDrawingRequest pagesDrawingRequest)
        {
            try
            {
                await panel.Dispatcher.BeginInvoke(new Action(() =>
                {
                    DrawOnWritableBitmap(panel, surface, pagesDrawingRequest);
                })).Task.ConfigureAwait(false);
            }
            catch
            {
                // Application is closed when this task is running, ignore
            }
        }

        private static void DrawOnWritableBitmap(this SkiaPdfPanel panel, SKSurface sourceSurface, PagesDrawingRequest pagesDrawingRequest)
        {
            WriteableBitmap writeableBitmap = pagesDrawingRequest.WritableBitmap;
            writeableBitmap.Lock();

            SKImageInfo imageInfo = new SKImageInfo(writeableBitmap.PixelWidth, writeableBitmap.PixelHeight, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            SKSurface drawingSurface = SKSurface.Create(new SKImageInfo(writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            SKSurface surface = SKSurface.Create(imageInfo, writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride);

            using (drawingSurface)
            {
                using (surface)
                {
                    surface.Canvas.DrawSurface(sourceSurface, SKPoint.Empty);
                    pagesDrawingRequest.Pages.OnAfterDraw?.Invoke(drawingSurface.Canvas, pagesDrawingRequest.VisiblePages, pagesDrawingRequest.Scale);
                    surface.Canvas.DrawSurface(drawingSurface, SKPoint.Empty);
                }
            }

            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, imageInfo.Width, imageInfo.Height));

            var drawingVisual = panel.DrawingVisual;
            System.Windows.Media.DrawingContext render = drawingVisual.RenderOpen();

            var pixelOffsetX = panel.SnapPosition(pagesDrawingRequest.CanvasOffset.X, pagesDrawingRequest.CanvasScale.X);
            var pixelOffsetY = panel.SnapPosition(pagesDrawingRequest.CanvasOffset.Y, pagesDrawingRequest.CanvasScale.Y);

            render.DrawImage(writeableBitmap, new Rect(pixelOffsetX, pixelOffsetY, writeableBitmap.Width, writeableBitmap.Height));

            render.Close();

            writeableBitmap.Unlock();
        }
    }
}
