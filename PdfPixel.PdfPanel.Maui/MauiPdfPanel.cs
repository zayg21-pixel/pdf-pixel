using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using PdfPixel.Geometry;
using PdfPixel.Navigation;
using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Skia;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System;
using System.Diagnostics.CodeAnalysis;

namespace PdfPixel.PdfPanel.Maui;

/// <summary>
/// Represents a view that displays a PDF document using SkiaSharp.
/// </summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable")]
public partial class MauiPdfPanel : SKCanvasView
{
    private PdfPanelContext? _context;
    private IPdfPanelRenderTargetFactory? _renderTargetFactory;
    private CpuSkSurfaceFactory? _surfaceFactory;
    private SKImage? _presentedImage;
    private PdfPanelFrame? _presentedFrame;
    private bool _isRedrawPending;
    private bool _updatingScale;
    private bool _updatingPages;
    private PdfDestination? _pendingDestination;

    /// <summary>
    /// Initializes a new <see cref="MauiPdfPanel"/>.
    /// </summary>
    public MauiPdfPanel()
    {
        EnableTouchEvents = true;
        PageLayout = new PdfPanelVerticalLayout();
        Configuration = new PdfPanelConfiguration();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Raised after the panel's update for an action the panel does not perform.
    /// </summary>
    public event EventHandler<PdfPanelActionEventArgs>? ActionRequested;

    /// <summary>
    /// Size of the panel surface in device pixels.
    /// </summary>
    internal PdfSize PanelSize { get; private set; }

    /// <summary>
    /// Scale from host coordinates to device pixels.
    /// </summary>
    internal float HostScale { get; private set; } = 1;

    /// <summary>
    /// Scrolls to the page with the specified 1-based number.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    public void ScrollToPage(int pageNumber)
    {
        _context?.ScrollToPage(pageNumber);
        RequestRedraw();
    }

    /// <summary>
    /// Increases the zoom level by <see cref="PdfPanelConfiguration.ZoomStep"/>.
    /// </summary>
    public void ZoomIn()
    {
        _context?.ZoomIn();
        RequestRedraw();
    }

    /// <summary>
    /// Decreases the zoom level by <see cref="PdfPanelConfiguration.ZoomStep"/>.
    /// </summary>
    public void ZoomOut()
    {
        _context?.ZoomOut();
        RequestRedraw();
    }

    /// <summary>
    /// Shows <paramref name="image"/> rendered for <paramref name="frame"/> on the next paint of the canvas.
    /// </summary>
    internal void Present(SKImage image, PdfPanelFrame frame)
    {
        _presentedImage?.Dispose();
        _presentedImage = image;
        _presentedFrame = frame;

        InvalidateSurface();
    }

    /// <inheritdoc />
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        HostScale = (float)DeviceDisplay.Current.MainDisplayInfo.Density;
        PanelSize = new PdfSize((float)Math.Round(width * HostScale), (float)Math.Round(height * HostScale));

        RequestRedraw();
    }

    /// <inheritdoc />
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        base.OnPaintSurface(e);

        SKCanvas canvas = e.Surface.Canvas;
        canvas.Clear((Configuration ?? PdfPanelConfiguration.Default).PanelBackgroundColor.ToSkiaColor());

        if (_presentedImage == null || _presentedFrame == null)
        {
            return;
        }

        canvas.DrawImage(_presentedImage, 0, 0, SKSamplingOptions.Default);
        PanelInterface?.OnAfterDraw?.Invoke(canvas, _presentedFrame);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _surfaceFactory = new CpuSkSurfaceFactory(SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        _renderTargetFactory = new MauiPdfPanelRenderTargetFactory(this);

        RequestRedraw();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        DisposeContext();
        _context = null;

        _surfaceFactory?.Dispose();
        _surfaceFactory = null;
        _renderTargetFactory = null;

        _presentedImage?.Dispose();
        _presentedImage = null;
        _presentedFrame = null;
    }

    private void ResetContent()
    {
        ZoomScale = 1;
        CurrentPage = 1;

        _context?.Reset();
    }

    /// <summary>
    /// Schedules one update and render of the panel on the dispatcher, merging the requests made until it runs.
    /// </summary>
    private void RequestRedraw()
    {
        if (_isRedrawPending || !IsLoaded)
        {
            return;
        }

        _isRedrawPending = true;
        Dispatcher.Dispatch(Redraw);
    }

    private void Redraw()
    {
        _isRedrawPending = false;

        if (!CanRedraw())
        {
            return;
        }

        Update();
        _context?.Render();
    }

    private void Update()
    {
        SynchronizeContext();

        _updatingPages = true;
        int newPage = GetCurrentPage();

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

    private void EnsureContext()
    {
        if (_context != null && _context.Document == Document)
        {
            return;
        }

        if (Document == null || _surfaceFactory == null || _renderTargetFactory == null)
        {
            return;
        }

        DisposeContext();

        _context = new PdfPanelContext(Document, _surfaceFactory, _renderTargetFactory);
        _context.AutoScaleMode = AutoScaleMode;
        _context.Actions.ActionRequested += OnActionRequested;

        if (_pendingDestination != null)
        {
            _context.Actions.ScrollToDestination(_pendingDestination);
            _pendingDestination = null;
        }
    }

    private void DisposeContext()
    {
        if (_context == null)
        {
            return;
        }

        _context.Actions.ActionRequested -= OnActionRequested;
        _context.Dispose();
    }

    private void OnConfigurationChanged(object? sender, EventArgs e) => RequestRedraw();

    /// <summary>
    /// Scrolls to <paramref name="destination"/> through the context of the current pages, or once that context is created.
    /// </summary>
    private void ScrollToDestination(PdfDestination destination)
    {
        if (_context == null || _context.Document != Document)
        {
            _pendingDestination = destination;
            RequestRedraw();
            return;
        }

        _context.Actions.ScrollToDestination(destination);
        RequestRedraw();
    }

    private void SynchronizeContext()
    {
        EnsureContext();

        if (_context == null)
        {
            return;
        }

        _context.PanelWidth = PanelSize.Width;
        _context.PanelHeight = PanelSize.Height;
        _context.Layout = PageLayout;
        (Configuration ?? PdfPanelConfiguration.Default).ApplyConfiguration(_context);

        _context.Synchronize();

        _updatingScale = true;
        ZoomScale = _context.Scale;
        AutoScaleMode = _context.AutoScaleMode;
        _updatingScale = false;
    }

    private bool CanRedraw()
    {
        return Document != null
            && PanelSize.Width > 1
            && PanelSize.Height > 1
            && IsLoaded
            && IsVisible;
    }

    private void HandleInterfaceRequest(object? sender, PdfPanelInterfaceActionEventArgs e)
    {
        switch (e.Action)
        {
            case PdfPanelInterfaceAction.ZoomIn:
                {
                    ZoomIn();
                    break;
                }
            case PdfPanelInterfaceAction.ZoomOut:
                {
                    ZoomOut();
                    break;
                }
            case PdfPanelInterfaceAction.RequestRedraw:
                {
                    RequestRedraw();
                    break;
                }
            case PdfPanelInterfaceAction.RequestPresent:
                {
                    _context?.Present();
                    break;
                }
            case PdfPanelInterfaceAction.ScrollToDestination when e.Destination != null:
                {
                    ScrollToDestination(e.Destination);
                    break;
                }
        }
    }

    private void OnActionRequested(object? sender, PdfPanelActionEventArgs e)
        => Dispatcher.Dispatch(() => ActionRequested?.Invoke(this, e));
}
