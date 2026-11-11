using PdfPixel.Annotations;
using PdfPixel.Geometry;
using PdfPixel.OptionalContent;
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
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Manages the panel, layout, and rendering state for a PDF panel viewer.
/// </summary>
public sealed class PdfPanelContext : IDisposable
{
    /// <summary>
    /// Default <see cref="ReferenceScale"/>: one inch of the page is 96 device pixels at a zoom of 100%.
    /// </summary>
    public const float DefaultReferenceScale = 96f / 72f;

    internal const float DefaultMinScale = 0.1f;
    internal const float DefaultMaxScale = 10f;
    internal const float DefaultZoomStep = 0.1f;
    private const float ScaleTolerance = 0.001f;
    private const float MaximumLookAheadPageToPanelRatio = 1.5f;

    private readonly IPdfPanelRenderTargetFactory _renderTargetFactory;
    private readonly PdfPanelScheduler _scheduler;
    private readonly PdfPanelGraphics _graphics;
    private readonly Timer _contentUpdateTimer;
    private IPdfPanelLayout _layout = new PdfPanelVerticalLayout();
    private ContentUpdateRequest? _sentContentRequest;
    private ContentUpdateRequest? _pendingContentRequest;
    private PagesDrawingRequest? _drawnRequest;
    private bool _presentRequested;
    private float? _lastVerticalOffset;
    private bool _isScrollingUp;

    /// <summary>
    /// Initializes the context with the given document and render target factory.
    /// Page and animation callbacks are posted to the synchronization context <paramref name="document"/> was created with,
    /// or invoked on the calling thread when it was created without one.
    /// </summary>
    public PdfPanelContext(
        PdfPanelDocument document,
        IPdfPanelRenderTargetFactory renderTargetFactory)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _renderTargetFactory = renderTargetFactory ?? throw new ArgumentNullException(nameof(renderTargetFactory));
        _scheduler = document.Scheduler;
        _scheduler.ClearExtractedContent();
        _graphics = new PdfPanelGraphics();

        Input = new PdfPanelInput();
        Annotations = new PdfPanelAnnotations(this, document, Input, _graphics);
        Text = new PdfPanelText(Document, Input, _graphics);
        Search = new PdfPanelSearch(this, Text, Document, _graphics);
        Content = new PdfPanelContent(Document, Input, _graphics);
        Layers = new PdfPanelLayers(Document);
        Thumbnails = new PdfPanelThumbnails(Document, _scheduler);
        Actions = new PdfPanelActions(this, Document, Annotations, Layers);
        Renderer = new PdfPanelRenderer(Document, _graphics);
        Document.OptionalContentStates = Layers.Configuration?.ToStates();
        _contentUpdateTimer = new Timer(OnContentUpdateDue, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

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
    /// Scale at which the pages are displayed at a zoom of 100%.
    /// </summary>
    public float ReferenceScale { get; set; } = DefaultReferenceScale;

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
        _scheduler.SetTextExtractionSettings(Text.ExtractText);
    }

    /// <summary>
    /// Renders the visible pages and starts producing the content of the visible pages and of the pages ahead of them.
    /// </summary>
    public void Render()
    {
        UpdateScrollDirection();

        RequestPageInfo[] visiblePages = GetVisiblePages().ToArray();
        RequestPageInfo[] lookAheadPages = GetLookAheadPages(visiblePages).ToArray();
        RequestPageInfo[] contentPages = visiblePages.Concat(lookAheadPages).ToArray();
        PdfAnnotationStates annotationStates = Annotations.CreateStates(contentPages, Scale, ReferenceScale);
        PagesDrawingRequest request = BuildRequest(visiblePages, annotationStates);
        Annotations.UpdateBubbles(request);
        Renderer.RedrawGraphics(_graphics.Update());

        if (Document.OptionalContentStates != request.OptionalContentStates)
        {
            Document.OptionalContentStates = request.OptionalContentStates;
            _scheduler.ClearExtractedContent();
        }

        if (_presentRequested || _drawnRequest == null || !IsSameFrame(_drawnRequest, request))
        {
            Renderer.Submit(request);
            _drawnRequest = request;
            _presentRequested = false;
        }

        UpdateContent(visiblePages.Length, contentPages, annotationStates);
    }

    /// <summary>
    /// Renders the visible pages as <see cref="Render"/> does, drawing the frame again even when nothing changed, so
    /// that <see cref="PdfPanelRenderer.OnPanelPageRendered"/> and <see cref="PdfPanelRenderer.OnPanelRendered"/> run
    /// again; content already produced is drawn from the cache.
    /// </summary>
    public void Present()
    {
        _presentRequested = true;
        Render();
    }

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

        _contentUpdateTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _sentContentRequest = null;
        _pendingContentRequest = null;
        _drawnRequest = null;
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

    /// <summary>
    /// Returns whether <paramref name="request"/> draws the same frame as <paramref name="drawnRequest"/>, the request
    /// drawn last: the same view, render target, pages and drawing settings.
    /// </summary>
    private static bool IsSameFrame(PagesDrawingRequest drawnRequest, PagesDrawingRequest request)
    {
        return drawnRequest.Scale == request.Scale
            && drawnRequest.ReferenceScale == request.ReferenceScale
            && drawnRequest.Offset == request.Offset
            && drawnRequest.PanelSize == request.PanelSize
            && drawnRequest.RenderTarget == request.RenderTarget
            && drawnRequest.Pages.SequenceEqual(request.Pages)
            && drawnRequest.Antialias == request.Antialias
            && drawnRequest.BackgroundColor.Equals(request.BackgroundColor)
            && drawnRequest.PageCornerRadius == request.PageCornerRadius
            && drawnRequest.ThumbnailPlaceholderMode == request.ThumbnailPlaceholderMode
            && drawnRequest.OptionalContentStates == request.OptionalContentStates
            && drawnRequest.AnnotationStates == request.AnnotationStates;
    }

    private PagesDrawingRequest BuildRequest(RequestPageInfo[] pages, PdfAnnotationStates annotationStates)
    {
        return new()
        {
            Scale = Scale,
            ReferenceScale = ReferenceScale,
            Offset = new PdfPoint(HorizontalOffset, VerticalOffset),
            PanelSize = new PdfSize(PanelWidth, PanelHeight),
            RenderTarget = _renderTargetFactory.GetRenderTarget(this),
            Pages = pages,
            Antialias = Renderer.Antialias,
            BackgroundColor = Renderer.BackgroundColor,
            PageCornerRadius = Renderer.PageCornerRadius,
            ThumbnailPlaceholderMode = Thumbnails.PlaceholderMode,
            OptionalContentStates = Layers.Configuration?.ToStates(),
            AnnotationStates = annotationStates
        };
    }

    /// <summary>
    /// Sends a content update request for <paramref name="pages"/>, the first <paramref name="visiblePageCount"/> of
    /// them visible, when something changed since the last request built: at once when it is the first, a visible page
    /// appeared or the annotation states changed, otherwise after the zoom or the scroll delay.
    /// </summary>
    private void UpdateContent(int visiblePageCount, RequestPageInfo[] pages, PdfAnnotationStates annotationStates)
    {
        ContentUpdateRequest? latestRequest = _sentContentRequest;

        if (_pendingContentRequest != null)
        {
            latestRequest = _pendingContentRequest;
        }

        PdfOptionalContentStates optionalContentStates = GetOptionalContentStates();
        bool settingsChanged = latestRequest == null || !HasSettings(latestRequest, optionalContentStates);
        bool annotationsChanged = latestRequest == null || !latestRequest.AnnotationStates.Equals(annotationStates);
        bool pagesChanged = latestRequest == null || latestRequest.Pages.Length != pages.Length;
        var visiblePageAppeared = false;
        var requestPages = new ContentUpdatePage[pages.Length];

        for (int pageIndex = 0; pageIndex < pages.Length; pageIndex++)
        {
            RequestPageInfo page = pages[pageIndex];
            bool isVisible = pageIndex < visiblePageCount;
            ContentUpdatePage? previousPage = latestRequest?.Pages.FirstOrDefault(requestPage => requestPage.Page.PageNumber == page.PageNumber);
            PdfRectangle visibleRegion;

            if (isVisible)
            {
                visibleRegion = page.VisibleRegion;
            }
            else
            {
                visibleRegion = PdfRectangle.FromLocationAndSize(0, 0, page.Page.Size.Width, page.Page.Size.Height);
            }

            if (previousPage == null)
            {
                pagesChanged = true;
                visiblePageAppeared = visiblePageAppeared || isVisible;
            }
            else if (previousPage.VisibleRegion != visibleRegion || previousPage.IsVisible != isVisible)
            {
                pagesChanged = true;
            }

            requestPages[pageIndex] = new ContentUpdatePage(page.Page, isVisible, visibleRegion);
        }

        if (!settingsChanged && !pagesChanged && !annotationsChanged)
        {
            return;
        }

        ContentUpdateRequest request = new(Scale, Renderer.TileSize, GetTileCapacity(), Renderer.Antialias, optionalContentStates, annotationStates, requestPages);
        TimeSpan contentUpdateDelay;

        if (latestRequest != null && latestRequest.Scale != Scale)
        {
            contentUpdateDelay = Renderer.ZoomContentUpdateDelay;
        }
        else
        {
            contentUpdateDelay = Renderer.ScrollContentUpdateDelay;
        }

        if (_sentContentRequest == null || visiblePageAppeared || annotationsChanged || contentUpdateDelay <= TimeSpan.Zero)
        {
            SendContentRequest(request);
        }
        else
        {
            _pendingContentRequest = request;
            _contentUpdateTimer.Change(contentUpdateDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Returns whether <paramref name="request"/> was built with the current scale, tile size, tile capacity,
    /// antialiasing and <paramref name="optionalContentStates"/>.
    /// </summary>
    private bool HasSettings(ContentUpdateRequest request, PdfOptionalContentStates optionalContentStates)
    {
        return request.Scale == Scale
            && request.TileSize == Renderer.TileSize
            && request.TileCapacity == GetTileCapacity()
            && request.Antialias == Renderer.Antialias
            && request.OptionalContentStates == optionalContentStates;
    }

    /// <summary>
    /// Returns how many tiles the panel needs: for the visible pages, the tiles a panel-sized region reaches, one column
    /// and one row more than it spans; for each look-ahead page, the tiles of the largest page looked ahead, a square
    /// of <see cref="MaximumLookAheadPageToPanelRatio"/> times the panel's larger side.
    /// </summary>
    private int GetTileCapacity()
    {
        int tileSize = Renderer.TileSize;
        int visibleColumns = (int)Math.Ceiling(PanelWidth / tileSize) + 1;
        int visibleRows = (int)Math.Ceiling(PanelHeight / tileSize) + 1;
        var lookAheadSide = (int)Math.Ceiling(Math.Max(PanelWidth, PanelHeight) * MaximumLookAheadPageToPanelRatio / tileSize);

        return (visibleColumns * visibleRows) + (Renderer.LookAheadPageCount * lookAheadSide * lookAheadSide);
    }

    private void SendContentRequest(ContentUpdateRequest request)
    {
        _sentContentRequest = request;
        _pendingContentRequest = null;
        _contentUpdateTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _scheduler.UpdateContent(request);
    }

    /// <summary>
    /// Returns the optional content states of the layers configuration, or of the document's default configuration
    /// when the layers have none.
    /// </summary>
    private PdfOptionalContentStates GetOptionalContentStates()
    {
        PdfUserOptionalContentConfiguration? configuration = Layers.Configuration;

        if (configuration != null)
        {
            return configuration.ToStates();
        }
        else
        {
            return Document.Document.OptionalContentProperties.GetDefaultStates();
        }
    }

    private void OnContentUpdateDue(object? state)
    {
        SynchronizationContext? synchronizationContext = Document.SynchronizationContext;

        if (synchronizationContext != null)
        {
            synchronizationContext.Post(OnContentUpdateDueSync, null);
        }
        else
        {
            OnContentUpdateDueSync(null);
        }
    }

    private void OnContentUpdateDueSync(object? state)
    {
        if (_pendingContentRequest == null)
        {
            return;
        }

        SendContentRequest(_pendingContentRequest);
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
    /// Returns up to <see cref="PdfPanelRenderer.LookAheadPageCount"/> pages following <paramref name="visiblePages"/> in the last scroll direction,
    /// leaving out the pages whose larger side is more than <see cref="MaximumLookAheadPageToPanelRatio"/> times the panel's larger side.
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

            RequestPageInfo page = CreateRequestPage(index);
            float pageSize = Math.Max(page.Page.Size.Width, page.Page.Size.Height) * Scale;
            float panelSize = Math.Max(PanelWidth, PanelHeight);

            if (pageSize <= panelSize * MaximumLookAheadPageToPanelRatio)
            {
                yield return page;
            }
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

        _contentUpdateTimer.Dispose();
        Annotations.Dispose();
        Text.Dispose();
        Content.Dispose();
        Renderer.Dispose();
    }
}
