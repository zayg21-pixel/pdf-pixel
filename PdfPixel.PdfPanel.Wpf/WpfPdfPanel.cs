using PdfPixel.Annotations.Models;
using System.Threading;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Wpf.Drawing;
using PdfPixel.PdfPanel.Wpf.OpenGl;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Animation;
using PdfPixel.PdfPanel.Annotations;

namespace PdfPixel.PdfPanel.Wpf;


/// <summary>
/// Represents a panel that displays a PDF document using SkiaSharp.
/// </summary>
public partial class WpfPdfPanel : FrameworkElement
{
    private readonly VisualCollection children;

    private PdfPanelContext _context;
    private PdfPanelRenderer _renderer;
    private PdfAnimationClock _animationClock;
    private IPdfPanelRenderTargetFactory _renderTargetFactory;
    private ISkSurfaceFactory _surfaceFactory;
    private bool _updatingScale;
    private bool _updatingPages;
    private PdfAnnotationPopup _lastAnnotationPopup;
    private PdfPanelPointerState _lastAnnotationState;

    public WpfPdfPanel()
    {
        UseLayoutRounding = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        children = new VisualCollection(this);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override int VisualChildrenCount => children.Count;

    internal DrawingVisual DrawingVisual { get; private set; }

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

        InputManager.Current.PreNotifyInput += OnPreNotifyInput;

        DrawingVisual = new DrawingVisual();
        children.Add(DrawingVisual);

        InvalidateVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var source = PresentationSource.FromVisual(this);
        ((HwndSource)source)?.RemoveHook(Hook);

        InputManager.Current.PreNotifyInput -= OnPreNotifyInput;

        _animationClock?.Dispose();
        _animationClock = null;
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

        var size = new Size(ActualWidth, ActualHeight);
        drawingContext.DrawRectangle(brush, null, new Rect(size));

        if (DrawingVisual != null)
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

        // TODO: [HIGH] there's no need to dispose it every time
        _renderer?.Dispose();
        _surfaceFactory?.Dispose();
        _animationClock?.Dispose();

        if (RenderMode == WpfRenderMode.OpenGl)
        {
            var glFactory = new OpenGlRenderTargetFactory(this, sampleCount: 1);
            _surfaceFactory = glFactory;
            _renderTargetFactory = glFactory;
        }
        else
        {
            _surfaceFactory = new CpuSkSurfaceFactory(SKColorType.Bgra8888, SKAlphaType.Premul);
            _renderTargetFactory = new WpfPdfPanelRenderTargetFactory(this);
        }

        _animationClock = new PdfAnimationClock();
        _renderer = new PdfPanelRenderer(_surfaceFactory, Pages.ContentProvider, _animationClock, SynchronizationContext.Current);
        _context = new PdfPanelContext(Pages, _renderer, _renderTargetFactory, new PdfPanelVerticalLayout());
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
        UpdatePointerState();

        _context.Update();

        UpdateAnnotationState();

        if (AnnotationPopup == null)
        {
            if (_renderer.TextSelector.IsPointerOverText)
            {
                Cursor = Cursors.IBeam;
            }
            else
            {
                Cursor = Cursors.Arrow;
            }
        }

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

        if (wasPressed && !isPressed)
        {
            HandleAnnotationClick(_lastAnnotationPopup);
        }

        UpdateAnnotationPopup(currentPopup);

        _lastAnnotationPopup = currentPopup;
        _lastAnnotationState = _context.ActiveAnnotationState;
    }

    private void UpdateAnnotationPopup(PdfAnnotationPopup currentPopup)
    {
        if (AnnotationPopup == currentPopup)
        {
            return;
        }

        AnnotationPopup = currentPopup;
        UpdateCursorForAnnotation(currentPopup);

        if (AnnotationToolTip != null)
        {
            if (currentPopup != null)
            {
                AnnotationToolTip.Content = AnnotationPopup;
            }

            AnnotationToolTip.IsOpen = AnnotationPopup != null && AnnotationPopup.Messages.Length > 0;
        }
    }

    private void UpdateCursorForAnnotation(PdfAnnotationPopup popup)
    {
        if (popup == null)
        {
            Cursor = Cursors.Arrow;
            return;
        }

        Cursor = popup.Navigation?.CursorType switch
        {
            PdfAnnotationCursorType.Hand => Cursors.Hand,
            PdfAnnotationCursorType.IBeam => Cursors.IBeam,
            _ => Cursors.Arrow
        };
    }

    private void HandleAnnotationClick(PdfAnnotationPopup popup)
    {
        if (popup.Navigation == null)
        {
            return;
        }

        switch (popup.Navigation.NavigationType)
        {
            case PdfAnnotationNavigationType.Uri:
                HandleUriAction(popup.Navigation.Uri);
                break;
            case PdfAnnotationNavigationType.GoToDestination:
                _context?.ScrollToDestination(popup.Navigation.Destination);
                InvalidateVisual();
                break;
            case PdfAnnotationNavigationType.GoToRemote:
                // TODO: handle remote file loading
                break;
        }
    }

    private void HandleUriAction(string uriString)
    {
        if (string.IsNullOrEmpty(uriString))
        {
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uriString,
                    UseShellExecute = true
                });
            }));
        }
#if DEBUG
        catch (Exception ex)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show($"Failed to open URI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }));
        }
#else
        catch (Exception)
        {
        }
#endif
    }
}