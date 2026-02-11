using PdfPixel.Annotations.Models;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Wpf.Drawing;
using SkiaSharp;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace PdfPixel.PdfPanel.Wpf
{

    /// <summary>
    /// Represents a panel that displays a PDF document using SkiaSharp.
    /// </summary>
    public partial class WpfPdfPanel : FrameworkElement
    {
        private readonly VisualCollection children;

        private PdfPanelContext _viewerContext;
        private PdfRenderingQueue renderingQueue;
        private IPdfPanelRenderTargetFactory renderTargetFactory;
        private bool _updatingScale;
        private bool _updatingPages;
        private PdfPanelPointerState _lastAnnotationState;
        private PdfAnnotationPopup _lastClickedAnnotation;
        private PdfWidgetAnnotation _focusedWidget;
        private PdfWidgetAnnotation _lastHoveredWidget;

        public WpfPdfPanel()
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
            _viewerContext?.Render();

            RaiseEvent(GetCanvasEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0), CanvasMouseMoveEvent));

            return base.ArrangeOverride(finalSize);
        }

        private void ResetContent()
        {
            // TODO: complete
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

        private void Update()
        {
            if (Pages == null)
            {
                return;
            }

            SyncViewerCanvasState();

            _updatingPages = true;
            CurrentPage = GetCurrentPage();
            _updatingPages = false;
        }

        private int GetCurrentPage()
        {
            if (_viewerContext != null)
            {
                return _viewerContext.GetCurrentPage();
            }
            return 0;
        }

        private void EnsureViewerCanvas()
        {
            if (_viewerContext != null && _viewerContext.Pages == Pages)
            {
                return;
            }

            renderingQueue?.Dispose();
            renderingQueue = new PdfRenderingQueue(new CpuSkSurfaceFactory());
            renderTargetFactory = new WpfPdfPanelRenderTargetFactory(this);
            _viewerContext = new PdfPanelContext(Pages, renderingQueue, renderTargetFactory, new PdfPanelVerticalLayout());
        }

        private void SyncViewerCanvasState()
        {
            EnsureViewerCanvas();

            _viewerContext.ViewportWidth = (float)CanvasSize.Width;
            _viewerContext.ViewportHeight = (float)CanvasSize.Height;
            _viewerContext.MinimumPageGap = (float)PageGap;
            _viewerContext.PagesPadding = new SKRect(
                (float)PagesPadding.Left,
                (float)PagesPadding.Top,
                (float)PagesPadding.Right,
                (float)PagesPadding.Bottom);

            var backgroundColor = BackgroundColor;
            _viewerContext.BackgroundColor = new SKColor(backgroundColor.R, backgroundColor.G, backgroundColor.B, backgroundColor.A);
            _viewerContext.MaxThumbnailSize = MaxThumbnailSize;

            _viewerContext.Update();

            _viewerContext.SetAutoScaleMode(AutoScaleMode);
            _viewerContext.Update();

            ExtentHeight = _viewerContext.ExtentHeight;
            ExtentWidth = _viewerContext.ExtentWidth;
            VerticalOffset = _viewerContext.VerticalOffset;
            HorizontalOffset = _viewerContext.HorizontalOffset;
            ViewportWidth = _viewerContext.ViewportWidth;
            ViewportHeight = _viewerContext.ViewportHeight;
            ExtentHeight = _viewerContext.ExtentHeight;
            ExtentWidth = _viewerContext.ExtentWidth;

            _updatingScale = true;
            Scale = _viewerContext.Scale;
            _updatingScale = false;

            ScrollOwner.InvalidateScrollInfo();
        }

        private bool CanRedraw()
        {
            return Pages != null &&
                this.IsCanvasSizeValid(CanvasSize) &&
                IsLoaded && IsVisible;
        }

        private void HandleInterfaceRequest(PdfPanelInterfaceAction action)
        {
            switch (action)
            {
                case PdfPanelInterfaceAction.ZoomIn:
                    ZoomIn();
                    break;

                case PdfPanelInterfaceAction.ZoomOut:
                    ZoomOut();
                    break;

                case PdfPanelInterfaceAction.RequestRedraw:
                    InvalidateVisual();
                    break;
            }
        }
    }
}