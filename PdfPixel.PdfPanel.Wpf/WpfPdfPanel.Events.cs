using PdfPixel.Annotations.Models;
using PdfPixel.Forms;
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

            SKMatrix matrix = page?.ViewportToPageMatrix(_viewerContext) ?? SKMatrix.Identity;
            SKPoint skiaPagePoint = matrix.MapPoint(request.PointerPosition.Value);

            bool isPressed = request.PointerState == PdfPanelPointerState.Pressed;
            bool isMouseUp = !isPressed && _lastAnnotationState == PdfPanelPointerState.Pressed;

            if (newPopup?.Annotation is PdfWidgetAnnotation widget)
            {
                if (_lastHoveredWidget != null && _lastHoveredWidget != widget)
                {
                    _lastHoveredWidget.HandleMouseLeave();
                    _lastHoveredWidget = null;
                }

                if (_lastHoveredWidget != widget)
                {
                    widget.HandleMouseEnter();
                    _lastHoveredWidget = widget;
                }

                SKPoint pdfPagePoint = ConvertSkiaPagePointToPdfPagePoint(skiaPagePoint, page);

                if (isPressed || isMouseUp)
                {
                    HandleWidgetAnnotationInteraction(widget, page, pdfPagePoint, request.PointerState, isMouseUp);
                }
                else
                {
                    HandleWidgetMouseMove(widget, pdfPagePoint, request.PointerState);
                }
            }
            else
            {
                if (_lastHoveredWidget != null)
                {
                    _lastHoveredWidget.HandleMouseLeave();
                    _lastHoveredWidget = null;
                }

                if (isPressed &&
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

        /// <summary>
        /// Converts a point from Skia page coordinates (top-left origin, Y down) to PDF page coordinates (bottom-left origin, Y up).
        /// </summary>
        /// <param name="skiaPagePoint">Point in Skia page coordinates.</param>
        /// <param name="page">The page for coordinate system reference.</param>
        /// <returns>Point in PDF page coordinates.</returns>
        private static SKPoint ConvertSkiaPagePointToPdfPagePoint(SKPoint skiaPagePoint, PdfPanelPage page)
        {
            if (page == null)
            {
                return skiaPagePoint;
            }

            return new SKPoint(
                skiaPagePoint.X,
                page.Info.Height - skiaPagePoint.Y);
        }

        private void UpdateCursorForAnnotation(PdfAnnotationPopup popup)
        {
            if (popup?.Annotation is PdfWidgetAnnotation widget)
            {
                Cursor = widget.GetCursorType() switch
                {
                    WidgetCursorType.Hand => Cursors.Hand,
                    WidgetCursorType.IBeam => Cursors.IBeam,
                    _ => Cursors.Arrow
                };
            }
            else if (popup?.Annotation is PdfLinkAnnotation || popup?.Annotation.ShouldDisplayBubble == true)
            {
                Cursor = Cursors.Hand;
            }
            else
            {
                Cursor = Cursors.Arrow;
            }
        }

        private void HandleWidgetAnnotationInteraction(PdfWidgetAnnotation widget, PdfPanelPage page, SKPoint pagePoint, PdfPanelPointerState pointerState, bool isMouseUp)
        {
            if (widget.Field == null)
            {
                return;
            }

            var formFieldState = ConvertToFormFieldPointerState(pointerState);

            if (isMouseUp)
            {
                bool handled = widget.HandleMouseUp(pagePoint, formFieldState);

                if (handled)
                {
                    if (widget.Field is PdfButtonFormField buttonField && buttonField.IsPushButton && widget.Action != null)
                    {
                        if (widget.Action is PdfUriAction uriAction)
                        {
                            HandleUriAction(uriAction);
                        }
                        else if (widget.Action is PdfGoToAction goToAction)
                        {
                            HandleGoToAction(goToAction);
                        }
                    }

                    if (widget.CanReceiveFocus && !widget.HasFocus)
                    {
                        SetFocusedWidget(widget);
                    }

                    InvalidateVisual();
                }
            }
            else
            {
                bool handled = widget.HandleMouseDown(pagePoint, formFieldState);

                if (handled)
                {
                    InvalidateVisual();
                }
            }
        }

        private void HandleWidgetMouseMove(PdfWidgetAnnotation widget, SKPoint pagePoint, PdfPanelPointerState pointerState)
        {
            if (widget.Field == null)
            {
                return;
            }

            var formFieldState = ConvertToFormFieldPointerState(pointerState);
            bool handled = widget.HandleMouseMove(pagePoint, formFieldState);

            if (handled)
            {
                InvalidateVisual();
            }
        }

        private void SetFocusedWidget(PdfWidgetAnnotation widget)
        {
            if (_focusedWidget != null && _focusedWidget != widget)
            {
                _focusedWidget.Blur();
            }

            _focusedWidget = widget;

            if (widget != null)
            {
                widget.Focus();
                Focusable = true;
                Focus();
            }
        }

        private static FormFieldPointerState ConvertToFormFieldPointerState(PdfPanelPointerState state)
        {
            return state switch
            {
                PdfPanelPointerState.Pressed => FormFieldPointerState.Pressed,
                _ => FormFieldPointerState.Hover
            };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_focusedWidget != null && _focusedWidget.HasFocus)
            {
                var key = ConvertToFormFieldKey(e.Key);
                var modifiers = ConvertToFormFieldModifiers(e.KeyboardDevice.Modifiers);

                if (_focusedWidget.HandleKeyDown(key, modifiers))
                {
                    e.Handled = true;
                    InvalidateVisual();
                }
            }
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            if (_focusedWidget != null && _focusedWidget.HasFocus && !string.IsNullOrEmpty(e.Text))
            {
                if (_focusedWidget.HandleTextInput(e.Text))
                {
                    e.Handled = true;
                    InvalidateVisual();
                }
            }
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            if (_focusedWidget != null)
            {
                _focusedWidget.Blur();
                _focusedWidget = null;
            }
        }

        private static FormFieldKey ConvertToFormFieldKey(Key key)
        {
            return key switch
            {
                Key.Enter => FormFieldKey.Enter,
                Key.Tab => FormFieldKey.Tab,
                Key.Escape => FormFieldKey.Escape,
                Key.Back => FormFieldKey.Backspace,
                Key.Delete => FormFieldKey.Delete,
                Key.Left => FormFieldKey.Left,
                Key.Right => FormFieldKey.Right,
                Key.Up => FormFieldKey.Up,
                Key.Down => FormFieldKey.Down,
                Key.Home => FormFieldKey.Home,
                Key.End => FormFieldKey.End,
                Key.PageUp => FormFieldKey.PageUp,
                Key.PageDown => FormFieldKey.PageDown,
                Key.Space => FormFieldKey.Space,
                _ => FormFieldKey.Unknown
            };
        }

        private static FormFieldKeyModifiers ConvertToFormFieldModifiers(ModifierKeys modifiers)
        {
            var result = FormFieldKeyModifiers.None;

            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                result |= FormFieldKeyModifiers.Shift;
            }

            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                result |= FormFieldKeyModifiers.Control;
            }

            if (modifiers.HasFlag(ModifierKeys.Alt))
            {
                result |= FormFieldKeyModifiers.Alt;
            }

            return result;
        }
    }
}

