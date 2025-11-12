using SkiaSharp;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Collections.Concurrent;

namespace PdfReader.Wpf.PdfPanel.Drawing
{
    /// <summary>
    /// Extensions for drawing on <see cref="SkiaPdfPanel"/>.
    /// </summary>
    internal static class SkiaPdfPanelExtensions
    {
        public static async Task<bool> ProcessPagesDrawing(
            this SkiaPdfPanel panel,
            SKBitmap bitmap,
            PagesDrawingRequest request,
            PagesDrawingRequest lastRequest,
            ConcurrentQueue<DrawingRequest> updateQueue)
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.ClipRect(canvas.LocalClipBounds);


            if (CanDrawCached(request))
            {
                DrawCached(bitmap, request, lastRequest);
                await DrawOnWritableBitmapAsync(panel, bitmap, request).ConfigureAwait(false);

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

            foreach (var picture in request.Pages.UpdateCacheAndGetPictures(extendedVisiblePages, request.Scale, request.MaxThumbnailSize))
            {
                if (!visiblePages.Contains(picture.PageNumber))
                {
                    continue;
                }

                canvas.DrawPageFromRequest(picture.PageNumber, request, PageDrawFlags.Background | PageDrawFlags.Content);
                await DrawOnWritableBitmapAsync(panel, bitmap, request).ConfigureAwait(false);

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

        private static void DrawCached(SKBitmap bitmap, PagesDrawingRequest request, PagesDrawingRequest lastRequest)
        {
            using var canvas = new SKCanvas(bitmap);

            using var copy = new SKBitmap(bitmap.Info);
            using var copyConvas = new SKCanvas(copy);
            copyConvas.DrawBitmap(bitmap, 0, 0);

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
                    var thumbnailRect = SKRect.Create(0, 0, thumbnail.Width, thumbnail.Height);

                    var samplingOption = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.Nearest);
                    canvas.DrawImage(thumbnail, thumbnailRect, destRect, samplingOption);
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
                canvas.DrawBitmap(copy, sourceRect, destRect);
            }
        }

        public static async Task DrawOnWritableBitmapAsync(this SkiaPdfPanel panel, SKBitmap bitmap, PagesDrawingRequest pagesDrawingRequest)
        {
            try
            {
                await panel.Dispatcher.BeginInvoke(new Action(() =>
                {
                    DrawOnWritableBitmap(panel, bitmap, pagesDrawingRequest);
                })).Task.ConfigureAwait(false);
            }
            catch
            {
                // Application is closed when this task is running, ignore
            }
        }

        private static void DrawOnWritableBitmap(this SkiaPdfPanel panel, SKBitmap bitmap, PagesDrawingRequest pagesDrawingRequest)
        {
            WriteableBitmap writeableBitmap = null;
            bool bitmapLocked = false;
            SKImageInfo imageInfo = default;

            try
            {
                writeableBitmap = pagesDrawingRequest.WritableBitmap;

                try
                {
                    writeableBitmap.Lock();
                    bitmapLocked = true;
                    imageInfo = new SKImageInfo(writeableBitmap.PixelWidth, writeableBitmap.PixelHeight, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
                }
                catch (Exception lockEx)
                {
                    panel.RaiseDrawingError(lockEx, $"Failed to lock WriteableBitmap or create SKImageInfo (Size: {writeableBitmap?.PixelWidth}x{writeableBitmap?.PixelHeight})");
                    return;
                }

                SKSurface drawingSurface = null;
                SKSurface surface = null;
                try
                {
                    drawingSurface = SKSurface.Create(new SKImageInfo(writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                    if (drawingSurface == null)
                    {
                        throw new InvalidOperationException("Drawing surface creation returned null");
                    }

                    surface = SKSurface.Create(imageInfo, writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride);
                    if (surface == null)
                    {
                        throw new InvalidOperationException("Main surface creation returned null");
                    }
                }
                catch (Exception surfaceEx)
                {
                    drawingSurface?.Dispose();
                    panel.RaiseDrawingError(surfaceEx, $"Failed to create SKSurfaces (Size: {imageInfo.Width}x{imageInfo.Height}, Stride: {writeableBitmap.BackBufferStride})");
                    return;
                }

                using (drawingSurface)
                {
                    using (surface)
                    {
                        try
                        {
                            surface.Canvas.DrawBitmap(bitmap, SKRect.Create(bitmap.Width, bitmap.Height), SKRect.Create(imageInfo.Size));

                            try
                            {
                                pagesDrawingRequest.Pages.OnAfterDraw?.Invoke(drawingSurface.Canvas, pagesDrawingRequest.VisiblePages, pagesDrawingRequest.Scale);
                            }
                            catch (Exception callbackEx)
                            {
                                panel.RaiseDrawingError(callbackEx, "OnAfterDraw callback execution");
                            }

                            surface.Canvas.DrawSurface(drawingSurface, SKPoint.Empty);
                        }
                        catch (Exception drawEx)
                        {
                            panel.RaiseDrawingError(drawEx, $"Failed during drawing operations (Source: {bitmap.Width}x{bitmap.Height}, Target: {imageInfo.Width}x{imageInfo.Height})");
                            return;
                        }
                    }
                }

                try
                {
                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, imageInfo.Width, imageInfo.Height));
                    writeableBitmap.Unlock();
                    bitmapLocked = false;
                }
                catch (Exception bitmapFinalizationEx)
                {
                    panel.RaiseDrawingError(bitmapFinalizationEx, $"Failed to finalize WriteableBitmap (Size: {imageInfo.Width}x{imageInfo.Height})");
                    bitmapLocked = false;
                    return;
                }

                var drawingVisual = panel.DrawingVisual;
                System.Windows.Media.DrawingContext render = null;
                try
                {
                    render = drawingVisual.RenderOpen();
                    if (render == null)
                    {
                        throw new InvalidOperationException("RenderOpen returned null DrawingContext");
                    }

                    var pixelOffsetX = panel.SnapPosition(pagesDrawingRequest.CanvasOffset.X, pagesDrawingRequest.CanvasScale.X);
                    var pixelOffsetY = panel.SnapPosition(pagesDrawingRequest.CanvasOffset.Y, pagesDrawingRequest.CanvasScale.Y);

                    render.DrawImage(writeableBitmap, new Rect(pixelOffsetX, pixelOffsetY, writeableBitmap.Width, writeableBitmap.Height));
                }
                catch (Exception renderEx)
                {
                    panel.RaiseDrawingError(renderEx, $"Failed during WPF rendering (Size: {writeableBitmap?.Width}x{writeableBitmap?.Height})");
                    return;
                }
                finally
                {
                    try
                    {
                        render?.Close();
                    }
                    catch (Exception closeEx)
                    {
                        panel.RaiseDrawingError(closeEx, "Failed to close DrawingContext");
                    }
                }
            }
            catch (System.Runtime.InteropServices.SEHException sehEx)
            {
                panel.RaiseDrawingError(sehEx, "Native interop error during bitmap drawing");
            }
            catch (ObjectDisposedException disposedEx)
            {
                panel.RaiseDrawingError(disposedEx, "Object disposed during drawing operation");
            }
            catch (InvalidOperationException invalidEx)
            {
                panel.RaiseDrawingError(invalidEx, "Invalid operation during drawing");
            }
            catch (Exception ex)
            {
                panel.RaiseDrawingError(ex, "Unexpected error during drawing operation");
            }
            finally
            {
                if (bitmapLocked && writeableBitmap != null)
                {
                    try
                    {
                        writeableBitmap.Unlock();
                    }
                    catch (Exception unlockEx)
                    {
                        panel.RaiseDrawingError(unlockEx, "Failed to unlock WriteableBitmap in finally block");
                    }
                }
            }
        }
    }
}
