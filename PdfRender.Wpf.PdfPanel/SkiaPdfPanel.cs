using PdfRender.Canvas;
using PdfRender.Wpf.PdfPanel.Drawing;
using SkiaSharp;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PdfRender.Wpf.PdfPanel
{

    /// <summary>
    /// Represents a panel that displays a PDF document using SkiaSharp.
    /// </summary>
    public partial class SkiaPdfPanel : FrameworkElement
    {
        private readonly VisualCollection children;

        private PdfViewerCanvas _viewerCanvas;
        private PdfRenderingQueue renderingQueue;
        private ICanvasRenderTargetFactory renderTargetFactory;
        private bool updatingScale;

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

            //EnqueueRequest(GetBaseDrawingRequest<RefreshGraphicsDrawingRequest>());
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

            InvalidateVisual();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual(this);
            ((HwndSource)source)?.RemoveHook(Hook);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            (Size size, Point scale, Point offset) = this.MeasureCanvas(finalSize);

            CanvasSize = size;
            CanvasScale = scale;
            CanvasOffset = offset;

            if (!CanRedraw())
            {
                return base.ArrangeOverride(finalSize);
            }

            Update();
            _viewerCanvas?.Render();

            //RaiseEvent(GetCanvasEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0), CanvasMouseMoveEvent));

            return base.ArrangeOverride(finalSize);
        }

        private void ResetContent()
        {
            Scale = 1;
            CurrentPage = 1;
            HorizontalOffset = 0;
            VerticalOffset = 0;

            if (this.IsCanvasSizeValid(CanvasSize))
            {
                //EnqueueRequest(GetBaseDrawingRequest<ResetDrawingRequest>());
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
            if (_viewerCanvas != null)
            {
                return _viewerCanvas.CurrentPage;
            }
            return 0;
        }

        private void Update()
        {
            if (Pages == null)
            {
                return;
            }


            //if (Pages.CheckDocumentUpdates())
            //{
            //    var currentPage = CurrentPage;

            //    UpdateAutoScale();
            //    UpdateScrollInfo();
            //    ScrollToPage(currentPage);
            //}
            //else
            //{
            SyncViewerCanvasState();

            pageChangedLocally = true;
            CurrentPage = GetCurrentPage();
            pageChangedLocally = false;
        }

        private void EnsureViewerCanvas()
        {
            if (_viewerCanvas != null && _viewerCanvas.Pages == Pages)
            {
                return;
            }

            renderingQueue?.Dispose();
            renderingQueue = new PdfRenderingQueue(new CpuSkSurfaceFactory());
            renderTargetFactory = new SkiaPdfPanelRenderTargetFactory(this);
            _viewerCanvas = new PdfViewerCanvas(Pages, renderingQueue, renderTargetFactory);
        }

        private void SyncViewerCanvasState()
        {
            EnsureViewerCanvas();

            _viewerCanvas.Width = (float)CanvasSize.Width;
            _viewerCanvas.Height = (float)CanvasSize.Height;
            _viewerCanvas.MinimumPageGap = (float)PageGap;
            _viewerCanvas.PagesPadding = new SKRect(
                (float)PagesPadding.Left,
                (float)PagesPadding.Top,
                (float)PagesPadding.Right,
                (float)PagesPadding.Bottom);

            var backgroundColor = BackgroundColor;
            _viewerCanvas.BackgroundColor = new SKColor(backgroundColor.R, backgroundColor.G, backgroundColor.B, backgroundColor.A);
            _viewerCanvas.MaxThumbnailSize = MaxThumbnailSize;

            _viewerCanvas.Update();

            _viewerCanvas.SetAutoScaleMode(AutoScaleMode);
            _viewerCanvas.Update();

            ExtentHeight = _viewerCanvas.ExtentHeight;
            ExtentWidth = _viewerCanvas.ExtentWidth;
            VerticalOffset = _viewerCanvas.VerticalOffset;
            HorizontalOffset = _viewerCanvas.HorizontalOffset;
            ViewportWidth = _viewerCanvas.Width;
            ViewportHeight = _viewerCanvas.Height;
            ExtentHeight = _viewerCanvas.ExtentHeight;
            ExtentWidth = _viewerCanvas.ExtentWidth;

            updatingScale = true;
            Scale = _viewerCanvas.Scale;
            updatingScale = false;

            ScrollOwner.InvalidateScrollInfo();
        }

        private bool CanRedraw()
        {
            return Pages != null &&
                this.IsCanvasSizeValid(CanvasSize) &&
                IsLoaded && IsVisible;
        }
    }
}