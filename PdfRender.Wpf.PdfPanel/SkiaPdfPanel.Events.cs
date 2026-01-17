using PdfRender.Wpf.PdfPanel.Events;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace PdfRender.Wpf.PdfPanel
{
    public delegate void CanvasMouseEventHandler(object sender, CanvasMouseEventArgs e);

    public partial class SkiaPdfPanel
    {
        public static readonly RoutedEvent CanvasMouseMoveDownEvent = EventManager.RegisterRoutedEvent(
            nameof(CanvasMouseDown),
            RoutingStrategy.Bubble,
            typeof(CanvasMouseEventHandler),
            typeof(SkiaPdfPanel));

        public static readonly RoutedEvent CanvasMouseMoveUpEvent = EventManager.RegisterRoutedEvent(
            nameof(CanvasMouseUp),
            RoutingStrategy.Bubble,
            typeof(CanvasMouseEventHandler),
            typeof(SkiaPdfPanel));

        public static readonly RoutedEvent CanvasMouseMoveEvent = EventManager.RegisterRoutedEvent(
            nameof(CanvasMouseMove),
            RoutingStrategy.Bubble,
            typeof(CanvasMouseEventHandler),
            typeof(SkiaPdfPanel));

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

            if (Pages != null)
            {
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveDownEvent));
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (Pages != null)
            {
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveUpEvent));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (Pages != null)
            {
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveEvent));
            }
        }

        private CanvasMouseEventArgs GetCanvasEvent(MouseEventArgs args, RoutedEvent routedEvent)
        {
            VisiblePageInfo[] visiblePages = lastPagesDrawingRequest?.VisiblePages;
            Point position = args.GetPosition(this);
            Point relativePosition = GetCanvasPosition(position);
            VisiblePageInfo? pageInfo = visiblePages?.Select(x => (VisiblePageInfo?)x).FirstOrDefault(x => x.Value.GetScaledBounds(Scale).Contains(relativePosition));

            int? pageNumber = pageInfo?.PageNumber;
            Point? positionOnPage = pageInfo?.ToPagePosition(relativePosition, Scale);

            UpdateAnnotationPopup(pageInfo, pageNumber, positionOnPage);

            return new CanvasMouseEventArgs(routedEvent, this, relativePosition, pageNumber, positionOnPage, args);
        }

        private void UpdateAnnotationPopup(VisiblePageInfo? pageInfo, int? pageNumber, Point? positionOnPage)
        {
            AnnotationPopup newPopup;

            if (pageInfo.HasValue && Pages.TryGetPopup(pageNumber.Value, positionOnPage.Value, out var popup))
            {
                newPopup = popup;
            }
            else
            {
                newPopup = null;
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

        public static readonly RoutedEvent DrawingErrorEvent = EventManager.RegisterRoutedEvent(
            nameof(DrawingError),
            RoutingStrategy.Bubble,
            typeof(DrawingErrorEventHandler),
            typeof(SkiaPdfPanel));

        /// <summary>
        /// Occurs when an error happens during drawing operations.
        /// </summary>
        public event DrawingErrorEventHandler DrawingError
        {
            add { AddHandler(DrawingErrorEvent, value); }
            remove { RemoveHandler(DrawingErrorEvent, value); }
        }

        public void RaiseDrawingError(Exception exception, string context)
        {
            var args = new DrawingErrorEventArgs(DrawingErrorEvent, this, exception, context);
            RaiseEvent(args);
        }
    }
}
