using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Wpf.Drawing;
using PdfPixel.PdfPanel.Wpf.OpenGl;
using PdfPixel.Geometry;
using PdfPixel.Navigation.Model;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace PdfPixel.PdfPanel.Wpf;


/// <summary>
/// Represents a panel that displays a PDF document using SkiaSharp.
/// </summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable")]
public partial class WpfPdfPanel : FrameworkElement
{
    /// <summary>
    /// Identifies the <see cref="ActionRequested"/> routed event.
    /// </summary>
    public static readonly RoutedEvent ActionRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(ActionRequested),
        RoutingStrategy.Bubble,
        typeof(EventHandler<WpfPdfPanelActionEventArgs>),
        typeof(WpfPdfPanel));

    private readonly VisualCollection children;

    private PdfPanelContext? _context;
    private IPdfPanelRenderTargetFactory? _renderTargetFactory;
    private ISkSurfaceFactory? _surfaceFactory;
    private bool _updatingScale;
    private bool _updatingPages;
    private PdfDestination? _pendingDestination;

    /// <summary>
    /// Initializes a new <see cref="WpfPdfPanel"/>.
    /// </summary>
    public WpfPdfPanel()
    {
        UseLayoutRounding = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        children = new VisualCollection(this);
        SetValue(SearchResultsPropertyKey, new ObservableCollection<PdfPanelSearchMatch>());
        Layout = new PdfPanelVerticalLayout();
        Configuration = new PdfPanelConfiguration();
        Focusable = true;
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopyExecuted, OnCopyCanExecute));
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Raised after the panel's layout pass for an action the panel does not perform.
    /// </summary>
    public event EventHandler<WpfPdfPanelActionEventArgs> ActionRequested
    {
        add => AddHandler(ActionRequestedEvent, value);
        remove => RemoveHandler(ActionRequestedEvent, value);
    }

    /// <inheritdoc />
    protected override int VisualChildrenCount => children.Count;

    internal DrawingVisual? DrawingVisual { get; private set; }

    /// <summary>
    /// Size of the panel surface in device pixels.
    /// </summary>
    internal Size PanelSize { get; private set; }

    /// <summary>
    /// Scale from host coordinates to device pixels.
    /// </summary>
    internal Point HostScale { get; private set; }

    /// <summary>
    /// Position of the panel relative to the root visual, in host coordinates.
    /// </summary>
    internal Point HostOffset { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PresentationSource source = PresentationSource.FromVisual(this);
        ((HwndSource)source)?.AddHook(Hook);

        DrawingVisual = new DrawingVisual();
        children.Add(DrawingVisual);

        if (RenderMode == WpfRenderMode.OpenGl)
        {
            OpenGlRenderTargetFactory glFactory = new(this, sampleCount: 1);
            _surfaceFactory = glFactory;
            _renderTargetFactory = glFactory;
        }
        else
        {
            _surfaceFactory = new CpuSkSurfaceFactory(SKColorType.Bgra8888, SKAlphaType.Premul);
            _renderTargetFactory = new WpfPdfPanelRenderTargetFactory(this);
        }

        InvalidateVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PresentationSource source = PresentationSource.FromVisual(this);
        ((HwndSource)source)?.RemoveHook(Hook);

        DisposeContext();
        _context = null;

        _surfaceFactory?.Dispose();
        _surfaceFactory = null;
        _renderTargetFactory = null;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        (Size panelSize, Point hostScale, Point hostOffset) = this.MeasurePanel(finalSize);

        PanelSize = panelSize;
        HostScale = hostScale;
        HostOffset = hostOffset;

        if (!CanRedraw())
        {
            return base.ArrangeOverride(finalSize);
        }

        Update();
        _context?.Render();

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

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        if (drawingContext == null)
        {
            throw new ArgumentNullException(nameof(drawingContext));
        }

        SolidColorBrush brush = new(ToMediaColor((Configuration ?? PdfPanelConfiguration.Default).PanelBackgroundColor));
        brush.Freeze();

        Size size = new(ActualWidth, ActualHeight);
        drawingContext.DrawRectangle(brush, null, new Rect(size));

        if (DrawingVisual != null)
        {
            drawingContext.DrawDrawing(DrawingVisual.Drawing);
        }
    }

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => children[index];

    private void Update()
    {
        if (Pages == null || !IsLoaded)
        {
            return;
        }

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
        if (_context != null && _context.Pages == Pages)
        {
            return;
        }

        if (Pages == null || _surfaceFactory == null || _renderTargetFactory == null)
        {
            return;
        }

        DisposeContext();
        ClearSearchResults();

        _context = new PdfPanelContext(Pages, _surfaceFactory, _renderTargetFactory, SynchronizationContext.Current);
        _context.AutoScaleMode = AutoScaleMode;
        _context.Actions.ActionRequested += OnActionRequested;
        _context.Search.MatchesChanged += OnSearchMatchesChanged;
        _context.Text.TextExtracted += OnTextExtracted;
        SetValue(TextPropertyKey, _context.Text);
        SetValue(IsTextExtractedPropertyKey, _context.Text.IsTextExtracted);
        SetValue(ActionsPropertyKey, _context.Actions);
        PdfLayersViewModel layers = new(_context.Layers);
        layers.Changed += OnViewModelChanged;
        SetValue(LayersPropertyKey, layers);
        PdfOutlinesViewModel outlines = new(Pages, _context.Actions);
        outlines.Changed += OnViewModelChanged;
        SetValue(OutlinesPropertyKey, outlines);
        PdfThumbnailsViewModel thumbnails = new(_context.Thumbnails, Pages, _context.Actions);
        thumbnails.Changed += OnViewModelChanged;
        SetValue(ThumbnailsPropertyKey, thumbnails);
        PdfAttachmentsViewModel attachments = new(Pages);
        SetValue(AttachmentsPropertyKey, attachments);

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

        SetValue(TextPropertyKey, null);
        SetValue(IsTextExtractedPropertyKey, false);
        SetValue(ActionsPropertyKey, null);
        if (Layers != null)
        {
            Layers.Changed -= OnViewModelChanged;
        }

        SetValue(LayersPropertyKey, null);
        if (Outlines != null)
        {
            Outlines.Changed -= OnViewModelChanged;
        }

        SetValue(OutlinesPropertyKey, null);
        if (Thumbnails != null)
        {
            Thumbnails.Changed -= OnViewModelChanged;
        }

        SetValue(ThumbnailsPropertyKey, null);
        SetValue(AttachmentsPropertyKey, null);
        _context.Actions.ActionRequested -= OnActionRequested;
        _context.Search.MatchesChanged -= OnSearchMatchesChanged;
        _context.Text.TextExtracted -= OnTextExtracted;
        _context.Dispose();
    }

    private void OnTextExtracted(object? sender, EventArgs e) => SetValue(IsTextExtractedPropertyKey, true);

    private void OnViewModelChanged(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>
    /// Scrolls to <paramref name="destination"/> through the context of the current pages, or once that context is created.
    /// </summary>
    private void ScrollToDestination(PdfDestination destination)
    {
        if (_context == null || _context.Pages != Pages)
        {
            _pendingDestination = destination;
            return;
        }

        _context.Actions.ScrollToDestination(destination);
        InvalidateVisual();
    }

    private void SynchronizeContext()
    {
        EnsureContext();

        if (_context == null)
        {
            return;
        }

        _context.PanelWidth = (float)PanelSize.Width;
        _context.PanelHeight = (float)PanelSize.Height;
        _context.Layout = Layout;
        (Configuration ?? PdfPanelConfiguration.Default).ApplyConfiguration(_context);
        _context.Search.Query = SearchQuery;
        _context.Search.CurrentMatch = CurrentSearchResult;

        UpdatePointerState();

        _context.Synchronize();
        Layers?.Synchronize();

        UpdateAnnotationPopup(_context.Annotations.ActiveAnnotation);

        if (ActiveContentName != _context.Content.ActiveContentName)
        {
            SetValue(ActiveContentNamePropertyKey, _context.Content.ActiveContentName);
        }

        UpdateCursor();

        ExtentHeight = _context.ExtentHeight;
        ExtentWidth = _context.ExtentWidth;
        VerticalOffset = _context.VerticalOffset;
        HorizontalOffset = _context.HorizontalOffset;
        ViewportWidth = _context.PanelWidth;
        ViewportHeight = _context.PanelHeight;

        _updatingScale = true;
        Scale = _context.Scale;
        AutoScaleMode = _context.AutoScaleMode;
        _updatingScale = false;

        ScrollOwner?.InvalidateScrollInfo();
    }

    private bool CanRedraw()
    {
        return Pages != null
            && this.IsPanelSizeValid(PanelSize)
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
            case PdfPanelInterfaceAction.NextSearchResult:
                {
                    SelectNextSearchResult();
                    break;
                }
            case PdfPanelInterfaceAction.PreviousSearchResult:
                {
                    SelectPreviousSearchResult();
                    break;
                }
            case PdfPanelInterfaceAction.RequestRedraw:
                {
                    Update();
                    _context?.Render();
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

    private void UpdatePointerState()
    {
        if (_context == null)
        {
            return;
        }

        PdfPoint panelPoint = GetPanelPosition(Mouse.GetPosition(this));
        PdfPanelButtonState state = (Mouse.LeftButton == MouseButtonState.Pressed) ? PdfPanelButtonState.Pressed : PdfPanelButtonState.Default;

        _context.Input.PointerPosition = panelPoint;
        _context.Input.PointerState = state;
    }

    private PdfPoint GetPanelPosition(Point hostPosition)
    {
        PdfPoint hostScale = new((float)HostScale.X, (float)HostScale.Y);
        PdfPoint hostOffset = new((float)HostOffset.X, (float)HostOffset.Y);
        PdfMatrix hostToPanel = this.GetHostToPanelMatrix(hostOffset, hostScale);

        return hostToPanel.MapPoint(new PdfPoint((float)hostPosition.X, (float)hostPosition.Y));
    }

    private void UpdateCursor()
    {
        if (_context == null)
        {
            return;
        }

        Cursor = _context.Input.Cursor switch
        {
            PdfPanelCursor.Hand => Cursors.Hand,
            PdfPanelCursor.IBeam => Cursors.IBeam,
            _ => Cursors.Arrow
        };
    }

    private void UpdateAnnotationPopup(PdfAnnotationPopup? currentPopup)
    {
        if (AnnotationPopup == currentPopup)
        {
            return;
        }

        AnnotationPopup = currentPopup;

        if (AnnotationToolTip != null)
        {
            if (currentPopup != null)
            {
                AnnotationToolTip.Content = AnnotationPopup;
            }

            AnnotationToolTip.IsOpen = AnnotationPopup != null && AnnotationPopup.Messages.Count > 0;
        }
    }

    private void OnActionRequested(object? sender, PdfPanelActionEventArgs e)
        => Dispatcher.BeginInvoke(new Action<PdfPanelActionEventArgs>(RaiseActionRequested), e);

    private void RaiseActionRequested(PdfPanelActionEventArgs e) => RaiseEvent(new WpfPdfPanelActionEventArgs(ActionRequestedEvent, e));

    private static System.Windows.Media.Color ToMediaColor(in PdfPixel.Color.PdfColor color)
    {
        return System.Windows.Media.Color.FromArgb(
            ToColorByte(color.Alpha),
            ToColorByte(color.Red),
            ToColorByte(color.Green),
            ToColorByte(color.Blue));
    }

    private static byte ToColorByte(float component) => (byte)Math.Round(Math.Max(0f, Math.Min(1f, component)) * 255f);
}
