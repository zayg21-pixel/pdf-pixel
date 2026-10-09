using PdfPixel.Annotations.Model;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Wpf.Drawing;
using PdfPixel.PdfPanel.Wpf.OpenGl;
using PdfPixel.Geometry;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly VisualCollection children;

    private PdfPanelContext? _context;
    private IPdfPanelRenderTargetFactory? _renderTargetFactory;
    private ISkSurfaceFactory? _surfaceFactory;
    private bool _updatingScale;
    private bool _updatingPages;

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
        Focusable = true;
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopyExecuted, OnCopyCanExecute));
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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

        SolidColorBrush brush = new(BackgroundColor);
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
        _context.Search.MatchesChanged += OnSearchMatchesChanged;
        _context.Text.TextExtracted += OnTextExtracted;
        SetValue(TextPropertyKey, _context.Text);
        SetValue(IsTextExtractedPropertyKey, _context.Text.IsTextExtracted);
    }

    private void DisposeContext()
    {
        if (_context == null)
        {
            return;
        }

        SetValue(TextPropertyKey, null);
        SetValue(IsTextExtractedPropertyKey, false);
        _context.Search.MatchesChanged -= OnSearchMatchesChanged;
        _context.Text.TextExtracted -= OnTextExtracted;
        _context.Dispose();
    }

    private void OnTextExtracted(object? sender, EventArgs e) => SetValue(IsTextExtractedPropertyKey, true);

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
        _context.MinScale = (float)MinScale;
        _context.MaxScale = (float)MaxScale;
        _context.ZoomStep = (float)ZoomStep;
        _context.Renderer.BackgroundColor = ToPdfColor(BackgroundColor);
        _context.Renderer.PageCornerRadius = (float)PageCornerRadius;
        _context.Renderer.ShowPageLoadingAnimation = ShowPageLoadingAnimation;
        _context.Text.ExtractText = ExtractText;
        _context.Text.SelectionColor = ToPdfColor(SelectionColor);
        _context.Search.Query = SearchQuery;
        _context.Search.CurrentMatch = CurrentSearchResult;
        _context.Search.MatchColor = ToPdfColor(SearchMatchColor);
        _context.Search.CurrentMatchColor = ToPdfColor(SearchCurrentMatchColor);
        _context.Search.MatchCase = SearchMatchCase;
        _context.Search.WholeWord = SearchWholeWord;
        _context.Search.MatchDiacritics = SearchMatchDiacritics;

        UpdatePointerState();

        _context.Synchronize();

        if (_context.Annotations.ClickedAnnotation != null)
        {
            HandleAnnotationClick(_context.Annotations.ClickedAnnotation);
            _context.Synchronize();
        }

        UpdateAnnotationPopup(_context.Annotations.ActiveAnnotation);
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

    private void HandleInterfaceRequest(PdfPanelInterfaceAction action)
    {
        switch (action)
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

            AnnotationToolTip.IsOpen = AnnotationPopup != null && AnnotationPopup.Messages.Length > 0;
        }
    }

    private void HandleAnnotationClick(PdfAnnotationPopup popup)
    {
        if (popup.PageAnnotation?.Content is not PdfLinkAnnotation link)
        {
            return;
        }

        if (link.Action is PdfUriAction uriAction && uriAction.Uri != null)
        {
            HandleUriAction(uriAction.Uri.Value.ToString());
            return;
        }

        if (link.Action is PdfGoToAction goToAction)
        {
            PdfDestination? actionDestination = goToAction.GetDestination();

            if (actionDestination != null)
            {
                _context?.ScrollToDestination(actionDestination);
                InvalidateVisual();
                return;
            }
        }

        if (link.Action is PdfGoToRemoteAction)
        {
            // TODO: handle remote file loading
            return;
        }

        PdfDestination? linkDestination = link.GetDestination();

        if (linkDestination != null)
        {
            _context?.ScrollToDestination(linkDestination);
            InvalidateVisual();
        }
    }

    private void HandleUriAction(string uriString)
    {
        if (!Uri.TryCreate(uriString, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return;
        }

        Task.Run(() => LaunchUri(uri));
    }

    private void LaunchUri(Uri uri)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
#if DEBUG
        catch (Win32Exception ex)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show($"Failed to open URI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }));
        }
#else
        catch (Win32Exception)
        {
        }
#endif
    }

    private static PdfPixel.Color.PdfColor ToPdfColor(System.Windows.Media.Color color)
        => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
}
