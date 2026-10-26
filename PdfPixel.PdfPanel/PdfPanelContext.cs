using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Caching;
using PdfPixel.PdfPanel.Content;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Layers;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Scheduling;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Thumbnails;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Manages the panel, layout, and rendering state for a PDF panel viewer.
/// </summary>
public sealed class PdfPanelContext : IDisposable
{
    internal const float DefaultMinScale = 0.1f;
    internal const float DefaultMaxScale = 10f;
    internal const float DefaultZoomStep = 0.1f;
    private const float ScaleTolerance = 0.001f;

    private readonly IPdfPanelRenderTargetFactory _renderTargetFactory;
    private readonly PdfPanelScheduler _scheduler;
    private readonly PdfPanelGraphics _graphics;
    private IPdfPanelLayout _layout = new PdfPanelVerticalLayout();
    private float? _lastVerticalOffset;
    private bool _isScrollingUp;

    /// <summary>
    /// Initializes the context with the given document, surface factory and render target factory.
    /// Page and animation callbacks are posted to the synchronization context <paramref name="document"/> was created with,
    /// or invoked on the calling thread when it was created without one.
    /// </summary>
    public PdfPanelContext(
        PdfPanelDocument document,
        ISkSurfaceFactory surfaceFactory,
        IPdfPanelRenderTargetFactory renderTargetFactory)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _renderTargetFactory = renderTargetFactory ?? throw new ArgumentNullException(nameof(renderTargetFactory));
        _scheduler = document.Scheduler;
        _scheduler.ClearExtractedContent();
        _graphics = new PdfPanelGraphics(document);

        Input = new PdfPanelInput();
        Annotations = new PdfPanelAnnotations(document, Input);
        Text = new PdfPanelText(Document, Input, _graphics);
        Search = new PdfPanelSearch(this, Text, Document, _graphics);
        Content = new PdfPanelContent(Document, Input, _graphics);
        Layers = new PdfPanelLayers(Document);
        Thumbnails = new PdfPanelThumbnails(Document, _scheduler);
        Actions = new PdfPanelActions(this, Document, Annotations, Layers);
        Renderer = new PdfPanelRenderer(surfaceFactory, Document, _scheduler, _graphics);
        Document.OptionalContentStates = Layers.Configuration?.ToStates();

        Document.PageTextExtracted += OnPageTextExtracted;
        Document.PageThumbnailRendered += OnPageThumbnailRendered;
        Document.ExtractedContentCleared += OnExtractedContentCleared;
    }

    /// <summary>
    /// Width of the panel in device pixels (unscaled panel space).
    /// </summary>
    public float PanelWidth { get; set; }

    /// <summary>
    /// Height of the panel in device pixels (unscaled panel space).
    /// </summary>
    public float PanelHeight { get; set; }

    /// <summary>
    /// Layout that positions the pages within the panel.
    /// </summary>
    public IPdfPanelLayout Layout
    {
        get => _layout;
        set => _layout = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Minimum allowed zoom scale factor.
    /// </summary>
    public float MinScale { get; set; } = DefaultMinScale;

    /// <summary>
    /// Maximum allowed zoom scale factor.
    /// </summary>
    public float MaxScale { get; set; } = DefaultMaxScale;

    /// <summary>
    /// Proportional scale change of a single zoom step (e.g. 0.1 for 10%).
    /// </summary>
    public float ZoomStep { get; set; } = DefaultZoomStep;

    /// <summary>
    /// Total width of all pages including padding, in device pixels after applying <see cref="Scale"/>.
    /// </summary>
    public float ExtentWidth { get; private set; }

    /// <summary>
    /// Total height of all pages including padding, in device pixels after applying <see cref="Scale"/>.
    /// </summary>
    public float ExtentHeight { get; private set; }

    /// <summary>
    /// Vertical scroll offset in device pixels in the scaled panel space.
    /// A value of 0 means the top of the content is aligned with the top of the panel.
    /// </summary>
    public float VerticalOffset { get; set; }

    /// <summary>
    /// Horizontal scroll offset in device pixels in the scaled panel space.
    /// A value of 0 means the left of the content is aligned with the left of the panel.
    /// </summary>
    public float HorizontalOffset { get; set; }

    /// <summary>
    /// Current zoom factor. A value of 1.0 represents the natural size of the pages.
    /// Extent and offset values are expressed in the scaled space.
    /// </summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>
    /// Automatic scaling mode applied to <see cref="Scale"/> by <see cref="Synchronize"/>.
    /// </summary>
    public PdfPanelAutoScaleMode AutoScaleMode { get; set; }

    /// <summary>
    /// Document whose pages are displayed.
    /// </summary>
    public PdfPanelDocument Document { get; }

    /// <summary>
    /// Pointer and key input of the panel.
    /// </summary>
    public PdfPanelInput Input { get; }

    /// <summary>
    /// Text extraction and text selection of the panel.
    /// </summary>
    public PdfPanelText Text { get; }

    /// <summary>
    /// Search of the panel's text.
    /// </summary>
    public PdfPanelSearch Search { get; }

    /// <summary>
    /// Annotation under the pointer and its interaction state.
    /// </summary>
    public PdfPanelAnnotations Annotations { get; }

    /// <summary>
    /// Content item under the pointer and image selection.
    /// </summary>
    public PdfPanelContent Content { get; }

    /// <summary>
    /// Layers (optional content) configuration content is rendered with.
    /// </summary>
    public PdfPanelLayers Layers { get; }

    /// <summary>
    /// Page thumbnails of the panel, cleared when the visibility of a layer changes.
    /// </summary>
    public PdfPanelThumbnails Thumbnails { get; }

    /// <summary>
    /// Actions of the document's open action, of the current page's open and close actions and of activated links,
    /// and navigation to destinations.
    /// </summary>
    public PdfPanelActions Actions { get; }

    /// <summary>
    /// Appearance and rendering quality of the panel.
    /// </summary>
    public PdfPanelRenderer Renderer { get; }

    /// <summary>
    /// Gets the panel rectangle in scaled coordinate space.
    /// </summary>
    public PdfRectangle PanelRectangle => PdfRectangle.FromLocationAndSize(HorizontalOffset, VerticalOffset, PanelWidth, PanelHeight);

    /// <summary>
    /// Synchronizes the panel state with the current property values: recalculates dimensions and page positions,
    /// clamps scroll offsets, dispatches pointer input and performs the actions it triggers, together with the
    /// document's open action once the panel has a size and the close and open actions of the pages when the current
    /// page changes. Should be called after changing any property.
    /// </summary>
    public void Synchronize()
    {
        UpdateLayout();

        PdfPanelPointerPosition? pointerPosition = null;

        if (Input.PointerPosition != null)
        {
            pointerPosition = ResolvePointerPosition(Input.PointerPosition.Value);
        }

        Annotations.ClearClicked();
        Input.Synchronize(pointerPosition);

        Actions.Synchronize();

        Text.Synchronize();
        Search.Synchronize();
        Content.Synchronize(Scale);
        Renderer.Synchronize();
        _scheduler.SetTextExtractionSettings(Text.ExtractText);
    }

    /// <summary>
    /// Enqueues a rendering request for the visible pages to the rendering queue.
    /// </summary>
    public void Render()
    {
        Renderer.RedrawGraphics(_graphics.Update());

        UpdateScrollDirection();

        PagesDrawingRequest request = BuildRequest(GetVisiblePages().ToArray(), isLookAhead: false);
        PagesDrawingRequest lookAheadRequest = BuildRequest(GetLookAheadPages(request.Pages).ToArray(), isLookAhead: true);
        Actions.ClearAnnotationUpdate();

        if (Document.OptionalContentStates != request.OptionalContentStates)
        {
            Document.OptionalContentStates = request.OptionalContentStates;
            _scheduler.ClearExtractedContent();
        }

        Renderer.Submit(request);
        Renderer.Submit(lookAheadRequest);
    }

    /// <summary>
    /// Requests rendering without redrawing surface content to trigger <see cref="IPdfPanelRenderTarget.Render"/>.
    /// </summary>
    public void Present() => Renderer.Present();

    /// <summary>
    /// Increases the current scale by <see cref="ZoomStep"/> while preserving the panel offset around the panel center.
    /// </summary>
    public void ZoomIn() => ZoomIn(PanelWidth / 2, PanelHeight / 2);

    /// <summary>
    /// Increases the current scale by <see cref="ZoomStep"/> while preserving the panel offset around the provided center.
    /// </summary>
    /// <param name="centerX">X coordinate in panel space to preserve while zooming.</param>
    /// <param name="centerY">Y coordinate in panel space to preserve while zooming.</param>
    public void ZoomIn(float centerX, float centerY) => Zoom(Scale + (Scale * ZoomStep), centerX, centerY);

    /// <summary>
    /// Decreases the current scale by <see cref="ZoomStep"/> while preserving the panel offset around the panel center.
    /// </summary>
    public void ZoomOut() => ZoomOut(PanelWidth / 2, PanelHeight / 2);

    /// <summary>
    /// Decreases the current scale by <see cref="ZoomStep"/> while preserving the panel offset around the provided center.
    /// </summary>
    /// <param name="centerX">X coordinate in panel space to preserve while zooming.</param>
    /// <param name="centerY">Y coordinate in panel space to preserve while zooming.</param>
    public void ZoomOut(float centerX, float centerY) => Zoom(Scale - (Scale * ZoomStep), centerX, centerY);

    /// <summary>
    /// Sets <see cref="Scale"/> to <paramref name="scale"/> clamped to <see cref="MinScale"/> and <see cref="MaxScale"/>
    /// while preserving the panel offset around the provided center, and turns off <see cref="AutoScaleMode"/>.
    /// </summary>
    /// <param name="scale">The scale to apply.</param>
    /// <param name="centerX">X coordinate in panel space to preserve while zooming.</param>
    /// <param name="centerY">Y coordinate in panel space to preserve while zooming.</param>
    public void Zoom(float scale, float centerX, float centerY)
    {
        AutoScaleMode = PdfPanelAutoScaleMode.NoAutoScale;
        this.UpdateScalePreserveOffset(Clamp(scale, MinScale, MaxScale), centerX, centerY);
    }

    /// <summary>
    /// Resets visual state, cleans up rendering surface.
    /// </summary>
    public void Reset()
    {
        Input.Leave();
        Renderer.Reset();
    }

    /// <summary>
    /// Maps a panel position to the visible page it falls on.
    /// </summary>
    public PdfPanelPointerPosition ResolvePointerPosition(in PdfPoint panelPosition)
    {
        for (int i = 0; i < Document.Pages.Count; i++)
        {
            PdfPanelPage page = Document.Pages[i];

            if (!page.IsPageVisible(PanelRectangle, Scale))
            {
                continue;
            }

            PdfMatrix matrix = page.PanelToPageMatrix(Scale, HorizontalOffset, VerticalOffset);
            PdfPoint pagePosition = matrix.MapPoint(panelPosition);

            if (page.IsPointInPageBounds(pagePosition))
            {
                return new PdfPanelPointerPosition(panelPosition, new PdfPanelPagePoint(i + 1, pagePosition));
            }
        }

        return new PdfPanelPointerPosition(panelPosition, null);
    }

    /// <summary>
    /// Recalculates dimensions and page positions, scrolls to a recorded navigation target and clamps scroll offsets.
    /// </summary>
    internal void UpdateLayout()
    {
        Scale = Clamp(Scale, MinScale, MaxScale);

        ApplyAutoScale();

        PdfSize extentSize = Layout.CalculateDimensions(Document.Pages, Scale, PanelWidth, PanelHeight);

        ExtentWidth = extentSize.Width;
        ExtentHeight = extentSize.Height;

        Layout.CalculatePageOffsets(Document.Pages, Scale, ExtentWidth, ExtentHeight);

        Actions.ApplyNavigation();

        VerticalOffset = Clamp(VerticalOffset, 0, Math.Max(0, ExtentHeight - PanelHeight));
        HorizontalOffset = Clamp(HorizontalOffset, 0, Math.Max(0, ExtentWidth - PanelWidth));
    }

    private PagesDrawingRequest BuildRequest(RequestPageInfo[] pages, bool isLookAhead)
    {
        return new()
        {
            Scale = Scale,
            ActiveAnnotation = Annotations.ActiveAnnotation,
            ActiveAnnotationState = Annotations.ActiveAnnotationState,
            Offset = new PdfPoint(HorizontalOffset, VerticalOffset),
            PanelSize = new PdfSize(PanelWidth, PanelHeight),
            RenderTarget = _renderTargetFactory.GetRenderTarget(this),
            Pages = pages,
            IsLookAhead = isLookAhead,
            Antialias = Renderer.Antialias,
            SnapToDevicePixels = Renderer.SnapToDevicePixels,
            BackgroundColor = Renderer.BackgroundColor,
            PageCornerRadius = Renderer.PageCornerRadius,
            ShowPageLoadingAnimation = Renderer.ShowPageLoadingAnimation,
            ThumbnailPlaceholderMode = Thumbnails.PlaceholderMode,
            OptionalContentStates = Layers.Configuration?.ToStates(),
            ForceAnnotationUpdate = Actions.IsAnnotationUpdateRequested
        };
    }

    private IEnumerable<RequestPageInfo> GetVisiblePages()
    {
        for (int i = 0; i < Document.Pages.Count; i++)
        {
            if (Document.Pages[i].IsPageVisible(PanelRectangle, Scale))
            {
                yield return CreateRequestPage(i);
            }
        }
    }

    /// <summary>
    /// Returns up to <see cref="PdfPanelRenderer.LookAheadPageCount"/> pages following <paramref name="visiblePages"/> in the last scroll direction.
    /// </summary>
    private IEnumerable<RequestPageInfo> GetLookAheadPages(RequestPageInfo[] visiblePages)
    {
        if (visiblePages.Length == 0)
        {
            yield break;
        }

        int step = _isScrollingUp ? -1 : 1;
        int edgeIndex = _isScrollingUp ? visiblePages[0].PageNumber - 1 : visiblePages[visiblePages.Length - 1].PageNumber - 1;

        for (int offset = 1; offset <= Renderer.LookAheadPageCount; offset++)
        {
            int index = edgeIndex + (step * offset);

            if (index < 0 || index >= Document.Pages.Count)
            {
                yield break;
            }

            yield return CreateRequestPage(index);
        }
    }

    private void UpdateScrollDirection()
    {
        if (_lastVerticalOffset != null && VerticalOffset != _lastVerticalOffset.Value)
        {
            _isScrollingUp = VerticalOffset < _lastVerticalOffset.Value;
        }

        _lastVerticalOffset = VerticalOffset;
    }

    private RequestPageInfo CreateRequestPage(int index)
    {
        PdfPanelPage page = Document.Pages[index];
        float offsetX = (page.Offset.X - HorizontalOffset) / Scale;
        float offsetY = (page.Offset.Y - VerticalOffset) / Scale;

        return new RequestPageInfo(
            page,
            new PdfPoint(offsetX, offsetY),
            new PdfSize(PanelWidth, PanelHeight),
            Scale,
            Renderer.TileSize);
    }

    private void ApplyAutoScale()
    {
        if (AutoScaleMode == PdfPanelAutoScaleMode.NoAutoScale || Document.Pages.Count == 0)
        {
            return;
        }

        PdfSize fitSize = Layout.CalculateFitSize(Document.Pages);
        float fitScale;

        if (AutoScaleMode == PdfPanelAutoScaleMode.ScaleToWidth)
        {
            float padding = Layout.Padding.Left + Layout.Padding.Right;
            fitScale = (PanelWidth - padding) / fitSize.Width;
        }
        else
        {
            fitScale = (PanelHeight - Layout.PageGap) / fitSize.Height;
        }

        float scale = Clamp(fitScale, MinScale, MaxScale);

        if (Math.Abs(scale - Scale) / Scale <= ScaleTolerance)
        {
            return;
        }

        this.UpdateScalePreserveOffset(scale, 0, 0);
    }

    private void OnPageTextExtracted(object? sender, PageTextExtractedEventArgs args)
    {
        Text.OnPageTextExtracted(args.PageNumber);
        Search.SearchPage(args.PageNumber);
        Renderer.RedrawGraphics(_graphics.Update());
    }

    private void OnPageThumbnailRendered(object? sender, PageThumbnailRenderedEventArgs args) => Thumbnails.OnPageThumbnailRendered(args);

    private void OnExtractedContentCleared(object? sender, ExtractedContentClearedEventArgs args)
    {
        if ((args.ContentTypes & CachedContentType.Words) != 0)
        {
            Text.OnExtractedContentCleared();
            Search.OnExtractedContentCleared();
            Renderer.RedrawGraphics(_graphics.Update());
        }

        if ((args.ContentTypes & CachedContentType.Thumbnail) != 0)
        {
            Thumbnails.OnExtractedContentCleared();
        }
    }

    private static float Clamp(float value, float min, float max)
        => Math.Max(min, Math.Min(max, value));

    /// <inheritdoc />
    public void Dispose()
    {
        Document.PageTextExtracted -= OnPageTextExtracted;
        Document.PageThumbnailRendered -= OnPageThumbnailRendered;
        Document.ExtractedContentCleared -= OnExtractedContentCleared;

        Annotations.Dispose();
        Text.Dispose();
        Content.Dispose();
        Renderer.Dispose();
        _graphics.Dispose();
    }
}
