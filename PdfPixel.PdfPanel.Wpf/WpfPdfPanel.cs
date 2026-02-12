using PdfPixel.Annotations.Models;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Wpf.Drawing;
using SkiaSharp;
using System;
using System.Diagnostics;
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

        private PdfPanelContext _context;
        private PdfRenderingQueue renderingQueue;
        private IPdfPanelRenderTargetFactory renderTargetFactory;
        private bool _updatingScale;
        private bool _updatingPages;
        private PdfAnnotationPopup _lastAnnotationPopup;
        private PdfPanelPointerState _lastAnnotationState;

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
            _context?.Render();

            RaiseEvent(GetCanvasEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0), CanvasMouseMoveEvent));

            return base.ArrangeOverride(finalSize);
        }

        private void ResetContent()
        {
            Scale = 1;
            CurrentPage = 1;
            HorizontalOffset = 0;
            VerticalOffset = 0;

            _context?.Reset();
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
            var newPage = GetCurrentPage();

            if (newPage != CurrentPage)
            {
                CurrentPage = newPage;
            }

            _updatingPages = false;
        }

        private int GetCurrentPage()
        {
            if (_context != null)
            {
                return _context.GetCurrentPage();
            }
            return 0;
        }

        private void EnsureViewerCanvas()
        {
            if (_context != null && _context.Pages == Pages)
            {
                return;
            }

            renderingQueue?.Dispose();
            renderingQueue = new PdfRenderingQueue(new CpuSkSurfaceFactory());
            renderTargetFactory = new WpfPdfPanelRenderTargetFactory(this);
            _context = new PdfPanelContext(Pages, renderingQueue, renderTargetFactory, new PdfPanelVerticalLayout());
        }

        private void SyncViewerCanvasState()
        {
            EnsureViewerCanvas();

            _context.ViewportWidth = (float)CanvasSize.Width;
            _context.ViewportHeight = (float)CanvasSize.Height;
            _context.MinimumPageGap = (float)PageGap;
            _context.PagesPadding = new SKRect(
                (float)PagesPadding.Left,
                (float)PagesPadding.Top,
                (float)PagesPadding.Right,
                (float)PagesPadding.Bottom);
            _context.PageCornerRadius = (float)PageCornerRadius;

            var backgroundColor = BackgroundColor;
            _context.BackgroundColor = new SKColor(backgroundColor.R, backgroundColor.G, backgroundColor.B, backgroundColor.A);
            _context.MaxThumbnailSize = MaxThumbnailSize;

            UpdatePointerState();

            _context.Update();

            UpdateAnnotationState();

            _context.SetAutoScaleMode(AutoScaleMode);
            _context.Update();

            ExtentHeight = _context.ExtentHeight;
            ExtentWidth = _context.ExtentWidth;
            VerticalOffset = _context.VerticalOffset;
            HorizontalOffset = _context.HorizontalOffset;
            ViewportWidth = _context.ViewportWidth;
            ViewportHeight = _context.ViewportHeight;
            ExtentHeight = _context.ExtentHeight;
            ExtentWidth = _context.ExtentWidth;

            _updatingScale = true;
            Scale = _context.Scale;
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
                    Update();
                    _context?.Render();
                    break;
                case PdfPanelInterfaceAction.RequestRefresh:
                    _context?.Refresh();
                    break;
            }
        }

        private void UpdatePointerState()
        {
            Point position = Mouse.GetPosition(this);
            Point canvasPosition = GetCanvasPosition(position);
            var viewportPoint = new SKPoint((float)canvasPosition.X, (float)canvasPosition.Y);
            var state = Mouse.LeftButton == MouseButtonState.Pressed ? PdfPanelButtonState.Pressed : PdfPanelButtonState.Default;

            _context.PointerPosition = viewportPoint;
            _context.PointerState = state;
        }

        private void UpdateAnnotationState()
        {
            PdfAnnotationPopup currentPopup = _context.ActiveAnnotation;

            bool wasPressed = _lastAnnotationPopup != null && _lastAnnotationState == PdfPanelPointerState.Pressed;
            bool isPressed = currentPopup != null && _context.ActiveAnnotationState == PdfPanelPointerState.Pressed;

            if (wasPressed && !isPressed && _lastAnnotationPopup.Annotation is PdfLinkAnnotation linkAnnotation)
            {
                HandleLinkAnnotationClick(linkAnnotation);
            }

            UpdateAnnotationPopup(currentPopup);

            _lastAnnotationPopup = currentPopup;
            _lastAnnotationState = _context.ActiveAnnotationState;
        }

        private void UpdateAnnotationPopup(PdfAnnotationPopup currentPopup)
        {
            if (AnnotationPopup != currentPopup)
            {
                AnnotationPopup = currentPopup;

                UpdateCursorForAnnotation(currentPopup);

                if (AnnotationToolTip != null)
                {
                    if (currentPopup != null)
                    {
                        AnnotationToolTip.Content = AnnotationPopup;
                    }

                    AnnotationToolTip.IsOpen = AnnotationPopup != null;
                }
            }
        }

        private void UpdateCursorForAnnotation(PdfAnnotationPopup popup)
        {
            if (popup != null && popup.IsInteractive())
            {
                Cursor = Cursors.Hand;
            }
            else
            {
                Cursor = Cursors.Arrow;
            }
        }

        private void HandleLinkAnnotationClick(PdfLinkAnnotation linkAnnotation)
        {
            if (linkAnnotation.Action is PdfUriAction uriAction)
            {
                HandleUriAction(uriAction);
            }
            else if (linkAnnotation.Action is PdfGoToAction goToAction)
            {
                HandleGoToAction(goToAction);
            }
            else if (linkAnnotation.Destination != null)
            {
                _context?.ScrollToDestination(linkAnnotation.Destination);
                InvalidateVisual();
            }
        }

        private void HandleUriAction(PdfUriAction uriAction)
        {
            string uriString = uriAction.Uri.ToString();
            if (string.IsNullOrEmpty(uriString))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uriString,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
#if DEBUG
                MessageBox.Show($"Failed to open URI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
#endif
            }
        }

        private void HandleGoToAction(PdfGoToAction goToAction)
        {
            if (goToAction.Destination != null)
            {
                _context?.ScrollToDestination(goToAction.Destination);
                InvalidateVisual();
            }
        }
    }
}