using PdfReader.Wpf.PdfPanel.Drawing;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfReader.Wpf.PdfPanel
{
    /// <summary>
    /// Represents a panel that displays a PDF document using SkiaSharp.
    /// </summary>
    public partial class SkiaPdfPanel : FrameworkElement
    {
        private const bool UseGpuAcceleration = false;

        private readonly VisualCollection children;
        private ConcurrentQueue<DrawingRequest> updateQueue;
        private SemaphoreSlim queueSemaphore;

        private WriteableBitmap writeableBitmap = null;
        private PagesDrawingRequest lastPagesDrawingRequest;
        private bool isReadingFromQueue;

        private bool pageChangedLocally;

        public SkiaPdfPanel()
        {
            UseLayoutRounding = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);

            children = new VisualCollection(this)
            {
                DrawingVisual
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override int VisualChildrenCount => children.Count;

        internal DrawingVisual DrawingVisual { get; } = new DrawingVisual();

        /// <summary>
        /// Size of the drawing canvas.
        /// </summary>
        public Size CanvasSize { get; private set; }

        /// <summary>
        /// Scale of the drawing canvas.
        /// </summary>
        public Point CanvasScale { get; private set; }

        /// <summary>
        /// Absolute position of canvas relative to parent window.
        /// </summary>
        public Point CanvasOffset { get; private set; }

        /// <summary>
        /// Requests a redraw of the panel to invoke <see cref="PdfViewerPageCollection.OnAfterDraw"/> delegate.
        /// To redraw all pages use <see cref="UIElement.InvalidateVisual"/> method.
        /// </summary>
        public void RequestRedraw()
        {
            if (!CanRedraw())
            {
                return;
            }

            EnqueueRequest(GetBaseDrawingRequest<RefreshGraphicsDrawingRequest>());
        }

        /// <summary>
        /// Returns collection of pages that are visible on panel.
        /// </summary>
        /// <returns>Visible pages.</returns>
        public IEnumerable<VisiblePageInfo> GetVisiblePages()
        {
            if (Pages == null)
            {
                yield break;
            }

            double verticalOffset = -VerticalOffset / Scale + PagesPadding.Top;
            double horizontalOffset = -HorizontalOffset / Scale;

            var centerOffset = GetCenterOffset() / Scale;
            horizontalOffset += centerOffset;

            for (int i = 0; i < Pages.Count; i++)
            {
                var page = Pages[i];
                var rotatedSize = page.Info.GetRotatedSize(page.UserRotation);

                if (page.IsVisible(verticalOffset, CanvasSize.Height / Scale))
                {
                    var pageOffsetLeft = (ExtentWidth / Scale - rotatedSize.Width) / 2;
                    yield return new VisiblePageInfo(i + 1, new Point(horizontalOffset + pageOffsetLeft, verticalOffset), page.Info, page.UserRotation);
                }

                verticalOffset += rotatedSize.Height + PageGap;
            }
        }

        /// <summary>
        /// Returns the position on the canvas.
        /// </summary>
        /// <param name="position">Position point.</param>
        /// <returns>Position on canvas.</returns>
        public Point GetCanvasPosition(Point position)
        {
            return new Point(position.X * CanvasScale.X, position.Y * CanvasScale.Y);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual(this);
            ((HwndSource)source)?.AddHook(Hook);

            if (isReadingFromQueue)
            {
                return;
            }

            updateQueue = new ConcurrentQueue<DrawingRequest>();
            queueSemaphore = new SemaphoreSlim(0);
            StartReadFromQueue(queueSemaphore, updateQueue);

            InvalidateVisual();

            isReadingFromQueue = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual(this);
            ((HwndSource)source)?.RemoveHook(Hook);

            if (!isReadingFromQueue)
            {
                return;
            }

            queueSemaphore.Release();
            queueSemaphore.Dispose();
            queueSemaphore = null;
            updateQueue = null;

            isReadingFromQueue = false;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            (Size size, Point scale, Point offset) = this.MeasureCanvas(finalSize);

            if (size != CanvasSize || scale != CanvasScale)
            {
                CanvasSize = size;
                CanvasScale = scale;

                if (this.IsCanvasSizeValid(size))
                {
                    writeableBitmap = new WriteableBitmap((int)size.Width, (int)size.Height, 96.0 * scale.X, 96.0 * scale.Y, PixelFormats.Pbgra32, null);
                }
                else
                {
                    writeableBitmap = null;
                }
            }

            CanvasOffset = offset;

            if (!CanRedraw())
            {
                return base.ArrangeOverride(finalSize);
            }

            Update();
            RequestRedrawPages();

            RaiseEvent(GetCanvasEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0), CanvasMouseMoveEvent));

            return base.ArrangeOverride(finalSize);
        }

        private void ResetContent()
        {
            Scale = 1;
            CurrentPage = 1;
            HorizontalOffset = 0;
            VerticalOffset = 0;

            if (writeableBitmap != null && this.IsCanvasSizeValid(CanvasSize))
            {
                EnqueueRequest(GetBaseDrawingRequest<ResetDrawingRequest>());
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var brush = new SolidColorBrush(BackgroundColor);
            brush.Freeze();

            drawingContext.DrawRectangle(brush, null, new Rect(0, 0, ActualWidth, ActualHeight));
            drawingContext.DrawDrawing(DrawingVisual.Drawing);
        }

        protected override Visual GetVisualChild(int index)
        {
            return children[index];
        }

        private int GetCurrentPage()
        {
            double verticalOffset = -VerticalOffset / Scale;
            for (int i = 0; i < Pages.Count; i++)
            {
                var page = Pages[i];

                if (page.IsCurrent(verticalOffset, PageGap, CanvasSize.Height / Scale))
                {
                    return page.PageNumber;
                }

                verticalOffset += page.Info.GetRotatedSize(page.UserRotation).Height + PageGap;
            }

            return Pages.Count;
        }

        private void Update()
        {
            if (Pages == null)
            {
                return;
            }

            if (Pages.CheckDocumentUpdates())
            {
                var currentPage = CurrentPage;

                UpdateAutoScale();
                UpdateScrollInfo();
                ScrollToPage(currentPage);
            }
            else
            {
                UpdateAutoScale();
                UpdateScrollInfo();
            }

            ValidateMargins();

            pageChangedLocally = true;
            CurrentPage = GetCurrentPage();
            pageChangedLocally = false;
        }

        private async void StartReadFromQueue(SemaphoreSlim semaphore, ConcurrentQueue<DrawingRequest> queue)
        {
            SKSurface surface = null;
            PagesDrawingRequest activePagesDrawingRequest = null;
            PagesDrawingRequest previousPagesDrawingRequest = null;
            bool pagesUpdated = false;
            GRContext context = null;

            if (UseGpuAcceleration)
            {
                using var d3dContext = new VorticeDirect3DContext();
                using var backend = d3dContext.CreateBackendContext();
                context = GRContext.CreateDirect3D(backend);
            }

            while (true)
            {
                try
                {
                    try
                    {
                        await semaphore.WaitAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    if (!queue.TryDequeue(out var request))
                    {
                        break;
                    }

                    if (!queue.IsEmpty)
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
                        var newSurface = CreateSurface(surface, context, width, height);

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
                        surface = CreateSurface(null, context, width, height);
                        pagesUpdated = false;
                    }

                    if (pagesUpdated)
                    {
                        pagesUpdated = !await this.ProcessPagesDrawing(surface, activePagesDrawingRequest, previousPagesDrawingRequest, queue).ConfigureAwait(false);
                        previousPagesDrawingRequest = activePagesDrawingRequest;
                    }
                    else if (surface != null && activePagesDrawingRequest != null)
                    {
                        await this.DrawOnWritableBitmapAsync(surface, activePagesDrawingRequest).ConfigureAwait(false);
                    }
                }
                catch
                {
#if DEBUG
                    throw;
#endif
                }
            }

            context?.Dispose();
            surface?.Dispose();
        }

        private static SKSurface CreateSurface(SKSurface source, GRContext context, int width, int height)
        {
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());

            var result = GetBaseSurface(info, context);

            if (source != null)
            {
                result.Canvas.DrawSurface(source, SKPoint.Empty);
            }

            return result;
        }

        private static SKSurface GetBaseSurface(SKImageInfo info, GRContext context)
        {
            if (context != null)
            {
                return SKSurface.Create(context, false, info);
            }
            return SKSurface.Create(info);
        }

        private void RequestRedrawPages()
        {
            if (!CanRedraw())
            {
                return;
            }

            var drawingRequest = GetPagesDrawingRequest();

            if (!drawingRequest.Equals(lastPagesDrawingRequest))
            {
                lastPagesDrawingRequest = drawingRequest;
                EnqueueRequest(drawingRequest);
            }
        }

        private bool CanRedraw()
        {
            return Pages != null &&
                this.IsCanvasSizeValid(CanvasSize) &&
                writeableBitmap != null &&
                IsLoaded && IsVisible;
        }

        private void EnqueueRequest(DrawingRequest drawingRequest)
        {
            if (!isReadingFromQueue)
            {
                return;
            }

            try
            {
                updateQueue.Enqueue(drawingRequest);
                queueSemaphore.Release();
            }
            catch (ObjectDisposedException)
            {
                // request enqueued after the panel was unloaded
            }
        }

        private PagesDrawingRequest GetPagesDrawingRequest()
        {
            if (Pages == null)
            {
                return null;
            }

            var drawingRequest = GetBaseDrawingRequest<PagesDrawingRequest>();
            drawingRequest.Pages = Pages;
            drawingRequest.VisiblePages = GetVisiblePages().ToArray();
            drawingRequest.BackgroundColor = BackgroundColor;
            drawingRequest.MaxThumbnailSize = MaxThumbnailSize;

            return drawingRequest;
        }

        private T GetBaseDrawingRequest<T>() where T : DrawingRequest, new()
        {
            return new T
            {
                Scale = Scale,
                Offset = new Point(HorizontalOffset, VerticalOffset),
                CanvasSize = CanvasSize,
                CanvasScale = CanvasScale,
                CanvasOffset = CanvasOffset,
                WritableBitmap = writeableBitmap,
            };
        }
    }
}