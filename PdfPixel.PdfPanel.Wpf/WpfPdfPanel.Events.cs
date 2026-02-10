using PdfPixel.PdfPanel.Extensions;
using SkiaSharp;
using System.Windows;
using System.Windows.Input;

namespace PdfPixel.PdfPanel.Wpf
{
    public delegate void CanvasMouseEventHandler(object sender, CanvasMouseEventArgs e);

    public partial class WpfPdfPanel
    {
        public static readonly RoutedEvent CanvasMouseMoveDownEvent = EventManager.RegisterRoutedEvent(
            nameof(CanvasMouseDown),
            RoutingStrategy.Bubble,
            typeof(CanvasMouseEventHandler),
            typeof(WpfPdfPanel));

        public static readonly RoutedEvent CanvasMouseMoveUpEvent = EventManager.RegisterRoutedEvent(
            nameof(CanvasMouseUp),
            RoutingStrategy.Bubble,
            typeof(CanvasMouseEventHandler),
            typeof(WpfPdfPanel));

        public static readonly RoutedEvent CanvasMouseMoveEvent = EventManager.RegisterRoutedEvent(
            nameof(CanvasMouseMove),
            RoutingStrategy.Bubble,
            typeof(CanvasMouseEventHandler),
            typeof(WpfPdfPanel));

        /// <summary>
        /// Occurs when the mouse is moved over the canvas.
        /// </summary>
        public event CanvasMouseEventHandler CanvasMouseDown
        {
            add { AddHandler(CanvasMouseMoveDownEvent, value); }
            remove { RemoveHandler(CanvasMouseMoveDownEvent, value); }
        }

        /// <summary>
        /// Occurs when the mouse button is up the canvas.
        /// </summary>
        public event CanvasMouseEventHandler CanvasMouseUp
        {
            add { AddHandler(CanvasMouseMoveUpEvent, value); }
            remove { RemoveHandler(CanvasMouseMoveUpEvent, value); }
        }

        /// <summary>
        /// Occurs when the mouse button is down the canvas.
        /// </summary>

        public event CanvasMouseEventHandler CanvasMouseMove
        {
            add { AddHandler(CanvasMouseMoveEvent, value); }
            remove { RemoveHandler(CanvasMouseMoveEvent, value); }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (Pages != null && _viewerContext != null)
            {
                UpdatePointerState(e, PdfPanelPointerState.Pressed);
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveDownEvent));
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (Pages != null && _viewerContext != null)
            {
                UpdatePointerState(e, PdfPanelPointerState.Default);
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveUpEvent));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (Pages != null && _viewerContext != null)
            {
                Point position = e.GetPosition(this);
                Point canvasPosition = GetCanvasPosition(position);
                var viewportPoint = new SKPoint((float)canvasPosition.X, (float)canvasPosition.Y);

                _viewerContext.PointerPosition = viewportPoint;
                _viewerContext.Render();

                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveEvent));
            }
        }

        private void UpdatePointerState(MouseEventArgs e, PdfPanelPointerState state)
        {
            Point position = e.GetPosition(this);
            Point canvasPosition = GetCanvasPosition(position);
            var viewportPoint = new SKPoint((float)canvasPosition.X, (float)canvasPosition.Y);

            _viewerContext.PointerPosition = viewportPoint;
            _viewerContext.PointerState = state;
            _viewerContext.Render();
        }

        private CanvasMouseEventArgs GetCanvasEvent(MouseEventArgs args, RoutedEvent routedEvent)
        {
            Point position = args.GetPosition(this);
            Point canvasPosition = GetCanvasPosition(position);

            var viewportPoint = new SKPoint((float)canvasPosition.X, (float)canvasPosition.Y);

            PdfPanelPage page = _viewerContext?.GetPageAtViewportPoint(viewportPoint);

            int? pageNumber = page?.PageNumber;
            Point? positionOnPage = null;

            if (page != null)
            {
                SKMatrix matrix = page.ViewportToPageMatrix(_viewerContext);
                SKPoint pagePoint = matrix.MapPoint(viewportPoint);
                positionOnPage = new Point(pagePoint.X, pagePoint.Y);
            }

            UpdateAnnotationPopup(page, pageNumber, positionOnPage);

            return new CanvasMouseEventArgs(routedEvent, this, canvasPosition, pageNumber, positionOnPage, args);
        }

        private void UpdateAnnotationPopup(PdfPanelPage page, int? pageNumber, Point? positionOnPage)
        {
            PdfAnnotationPopup newPopup = null;

            if (page != null && positionOnPage.HasValue)
            {
                var skPoint = new SKPoint((float)positionOnPage.Value.X, (float)positionOnPage.Value.Y);
                
                foreach (var popup in page.Popups)
                {
                    if (popup.Rect.Contains(skPoint))
                    {
                        newPopup = popup;
                        break;
                    }
                }
            }

            if (Equals(AnnotationPopup, newPopup))
            {
                return;
            }

            AnnotationPopup = newPopup;

            if (AnnotationToolTip != null)
            {
                if (newPopup != null)
                {
                    AnnotationToolTip.Content = AnnotationPopup;
                }

                AnnotationToolTip.IsOpen = AnnotationPopup != null;
            }
        }
    }
}

