using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Layout;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Text;
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
    private const float ScaleTolerance = 0.001f;

    private readonly IPdfPanelRenderTargetFactory _renderTargetFactory;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly PdfPageContentProvider _contentProvider;
    private readonly PdfPanelGraphics _graphics;
    private IPdfPanelLayout _layout = new PdfPanelVerticalLayout();

    /// <summary>
    /// Initializes the context with the given page collection, surface factory and render target factory.
    /// Page and animation callbacks are posted to <paramref name="synchronizationContext"/>, or invoked on the calling thread when it is <see langword="null"/>.
    /// </summary>
    public PdfPanelContext(
        PdfPanelPageCollection pages,
        ISkSurfaceFactory surfaceFactory,
        IPdfPanelRenderTargetFactory renderTargetFactory,
        SynchronizationContext? synchronizationContext)
    {
        Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _renderTargetFactory = renderTargetFactory ?? throw new ArgumentNullException(nameof(renderTargetFactory));
        _synchronizationContext = synchronizationContext;
        _contentProvider = pages.ContentProvider;
        _graphics = new PdfPanelGraphics(_contentProvider);

        Input = new PdfPanelInput();
        Text = new PdfPanelText(_contentProvider, Input, _graphics);
        Search = new PdfPanelSearch(this, Text, _contentProvider, _graphics);
        Annotations = new PdfPanelAnnotations(pages, Input);
        Layers = new PdfPanelLayers(_contentProvider.Document);
        Actions = new PdfPanelActions(this, _contentProvider.Document, Annotations, Layers);
        Renderer = new PdfPanelRenderer(surfaceFactory, _contentProvider, _graphics, synchronizationContext);

        _contentProvider.PageTextExtracted += OnPageTextExtracted;
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
    public float MinScale { get; set; } = 0.1f;

    /// <summary>
    /// Maximum allowed zoom scale factor.
    /// </summary>
    public float MaxScale { get; set; } = 10.0f;

    /// <summary>
    /// Proportional scale change of a single zoom step (e.g. 0.1 for 10%).
    /// </summary>
    public float ZoomStep { get; set; } = 0.1f;

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
    /// Collection of PDF pages to display.
    /// </summary>
    public PdfPanelPageCollection Pages { get; }

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
    /// Layers (optional content) configuration content is rendered with.
    /// </summary>
    public PdfPanelLayers Layers { get; }

    /// <summary>
    /// Actions of the document's open action and of activated links, and navigation to destinations.
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
    /// document's open action once the panel has a size. Should be called after changing any property.
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

        if (Actions.Synchronize())
        {
            UpdateLayout();
        }

        Text.Synchronize();
        Search.Synchronize();
        Renderer.Synchronize();
        _contentProvider.UpdateTextExtraction(Text.ExtractText);
    }

    /// <summary>
    /// Enqueues a rendering request for the visible pages to the rendering queue.
    /// </summary>
    public void Render()
    {
        Renderer.RedrawGraphics(_graphics.Update());
        Renderer.Submit(BuildRequest());
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
        for (int i = 0; i < Pages.Count; i++)
        {
            PdfPanelPage page = Pages[i];

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

    private void UpdateLayout()
    {
        Scale = Clamp(Scale, MinScale, MaxScale);

        ApplyAutoScale();

        PdfSize extentSize = Layout.CalculateDimensions(Pages, Scale, PanelWidth, PanelHeight);

        ExtentWidth = extentSize.Width;
        ExtentHeight = extentSize.Height;

        Layout.CalculatePageOffsets(Pages, Scale, ExtentWidth, ExtentHeight);

        Actions.ApplyNavigation();

        VerticalOffset = Clamp(VerticalOffset, 0, Math.Max(0, ExtentHeight - PanelHeight));
        HorizontalOffset = Clamp(HorizontalOffset, 0, Math.Max(0, ExtentWidth - PanelWidth));
    }

    private PagesDrawingRequest BuildRequest()
    {
        return new()
        {
            Scale = Scale,
            ActiveAnnotation = Annotations.ActiveAnnotation,
            ActiveAnnotationState = Annotations.ActiveAnnotationState,
            Offset = new PdfPoint(HorizontalOffset, VerticalOffset),
            PanelSize = new PdfSize(PanelWidth, PanelHeight),
            RenderTarget = _renderTargetFactory.GetRenderTarget(this),
            VisiblePages = GetVisiblePages().ToArray(),
            Antialias = Renderer.Antialias,
            SnapToDevicePixels = Renderer.SnapToDevicePixels,
            BackgroundColor = Renderer.BackgroundColor,
            PageCornerRadius = Renderer.PageCornerRadius,
            ShowPageLoadingAnimation = Renderer.ShowPageLoadingAnimation,
            OptionalContentStates = Layers.Configuration?.ToStates()
        };
    }

    private IEnumerable<VisiblePageInfo> GetVisiblePages()
    {
        PdfSize panelSize = new(PanelWidth, PanelHeight);

        for (int i = 0; i < Pages.Count; i++)
        {
            PdfPanelPage page = Pages[i];

            if (page.IsPageVisible(PanelRectangle, Scale))
            {
                float offsetX = (page.Offset.X - HorizontalOffset) / Scale;
                float offsetY = (page.Offset.Y - VerticalOffset) / Scale;
                yield return new VisiblePageInfo(
                    i + 1,
                    new PdfPoint(offsetX, offsetY),
                    page.Info,
                    page.UserRotation,
                    panelSize,
                    Scale,
                    Renderer.TileSize);
            }
        }
    }

    private void ApplyAutoScale()
    {
        if (AutoScaleMode == PdfPanelAutoScaleMode.NoAutoScale || Pages.Count == 0)
        {
            return;
        }

        float maxPageWidth = 0;
        float maxPageHeight = 0;

        foreach (PdfPanelPage page in Pages)
        {
            PdfSize rotatedSize = page.GetRotatedSize();
            maxPageWidth = Math.Max(maxPageWidth, rotatedSize.Width);
            maxPageHeight = Math.Max(maxPageHeight, rotatedSize.Height);
        }

        float fitScale;

        if (AutoScaleMode == PdfPanelAutoScaleMode.ScaleToWidth)
        {
            float padding = Layout.Padding.Left + Layout.Padding.Right;
            fitScale = (PanelWidth - padding) / maxPageWidth;
        }
        else
        {
            fitScale = (PanelHeight - Layout.PageGap) / maxPageHeight;
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
        if (_synchronizationContext != null)
        {
            _synchronizationContext.Post(_ => OnPageTextExtractedSync(args.PageNumber), null);
        }
        else
        {
            OnPageTextExtractedSync(args.PageNumber);
        }
    }

    private void OnPageTextExtractedSync(int pageNumber)
    {
        Text.OnPageTextExtracted(pageNumber);
        Search.SearchPage(pageNumber);
        Renderer.RedrawGraphics(_graphics.Update());
    }

    private static float Clamp(float value, float min, float max)
        => Math.Max(min, Math.Min(max, value));

    /// <inheritdoc />
    public void Dispose()
    {
        _contentProvider.PageTextExtracted -= OnPageTextExtracted;

        Annotations.Dispose();
        Text.Dispose();
        Renderer.Dispose();
        _graphics.Dispose();
    }
}
