using PdfPixel.Annotations.Models;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Diagnostics;
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
                UpdatePointerState(e);
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveDownEvent));
            }
        }

        private void HandleLinkAnnotationClick(PdfLinkAnnotation linkAnnotation, PdfPanelPage page)
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
                _viewerContext?.ScrollToDestination(linkAnnotation.Destination);
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
                _viewerContext?.ScrollToDestination(goToAction.Destination);
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (Pages != null && _viewerContext != null)
            {
                UpdatePointerState(e);
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveUpEvent));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (Pages != null && _viewerContext != null)
            {
                UpdatePointerState(e);
                RaiseEvent(GetCanvasEvent(e, CanvasMouseMoveEvent));
            }
        }

        private void UpdatePointerState(MouseEventArgs e)
        {
            Point position = e.GetPosition(this);
            Point canvasPosition = GetCanvasPosition(position);
            var viewportPoint = new SKPoint((float)canvasPosition.X, (float)canvasPosition.Y);
            var state = e.LeftButton == MouseButtonState.Pressed ? PdfPanelPointerState.Pressed : PdfPanelPointerState.Default;

            _viewerContext.PointerPosition = viewportPoint;
            _viewerContext.PointerState = state;
            InvalidateVisual();
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

            return new CanvasMouseEventArgs(routedEvent, this, canvasPosition, pageNumber, positionOnPage, args);
        }

        internal void UpdateAnnotationPopup(DrawingRequest request)
        {
            if (_viewerContext == null)
            {
                return;
            }

            if (request.PointerPosition == null)
            {
                return;
            }

            PdfPanelPage page = _viewerContext.GetPageAtViewportPoint(request.PointerPosition.Value);
            PdfAnnotationPopup newPopup = page?.ActivePopup;


            if (request.PointerState == PdfPanelPointerState.Pressed &&
                newPopup?.Annotation is PdfLinkAnnotation linkAnnotation &&
                _lastClickedAnnotation != newPopup && _lastAnnotationState != request.PointerState)
            {
                _lastClickedAnnotation = newPopup;
                HandleLinkAnnotationClick(linkAnnotation, page);
            }
            else if (request.PointerState == PdfPanelPointerState.Default)
            {
                _lastClickedAnnotation = null;
            }

            _lastAnnotationState = request.PointerState;

            if (Equals(AnnotationPopup, newPopup))
            {
                return;
            }

            AnnotationPopup = newPopup;

            UpdateCursorForAnnotation(newPopup);

            if (AnnotationToolTip != null)
            {
                if (newPopup != null)
                {
                    AnnotationToolTip.Content = AnnotationPopup;
                }

                AnnotationToolTip.IsOpen = AnnotationPopup != null;
            }
        }

        private void UpdateCursorForAnnotation(PdfAnnotationPopup popup)
        {
            if (popup?.Annotation is PdfLinkAnnotation || popup?.Annotation.ShouldDisplayBubble == true)
            {
                Cursor = Cursors.Hand;
            }
            else
            {
                Cursor = Cursors.Arrow;
            }
        }
    }
}

