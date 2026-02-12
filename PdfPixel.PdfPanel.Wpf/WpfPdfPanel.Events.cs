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

            if (Pages != null && _context != null)
            {
                InvalidateVisual();
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveDownEvent));
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (Pages != null && _context != null)
            {
                InvalidateVisual();
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveUpEvent));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (Pages != null && _context != null)
            {
                InvalidateVisual();
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveEvent));
            }
        }

        private CanvasMouseEventArgs GetCanvasEvent(MouseEventArgs args, RoutedEvent routedEvent)
        {
            Point position = args.GetPosition(this);
            Point canvasPosition = GetCanvasPosition(position);

            var viewportPoint = new SKPoint((float)canvasPosition.X, (float)canvasPosition.Y);

            PdfPanelPage page = _context?.GetPageAtViewportPoint(viewportPoint);

            int? pageNumber = page?.PageNumber;
            Point? positionOnPage = null;

            if (page != null)
            {
                SKMatrix matrix = page.ViewportToPageMatrix(_context);
                SKPoint pagePoint = matrix.MapPoint(viewportPoint);
                positionOnPage = new Point(pagePoint.X, pagePoint.Y);
            }

            return new CanvasMouseEventArgs(routedEvent, this, canvasPosition, pageNumber, positionOnPage, args);
        }
    }
}

