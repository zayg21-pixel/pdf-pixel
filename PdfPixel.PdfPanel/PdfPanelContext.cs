using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Settings;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Manages the panel, layout, and rendering state for a PDF panel viewer.
/// </summary>
public sealed class PdfPanelContext : IDisposable
{
    private const float ScaleTolerance = 0.001f;

    private readonly PdfPanelRenderer _renderer;
    private readonly IPdfPanelRenderTargetFactory _renderTargetFactory;
    private readonly PdfPanelAnnotationInteraction _annotationInteraction;

    /// <summary>
    /// Initializes the context with the given page collection, renderer, render target factory and settings.
    /// </summary>
    public PdfPanelContext(PdfPanelPageCollection pages, PdfPanelRenderer renderer, IPdfPanelRenderTargetFactory renderTargetFactory, PdfPanelSettings settings)
    {
        Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _renderTargetFactory = renderTargetFactory ?? throw new ArgumentNullException(nameof(renderTargetFactory));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _annotationInteraction = new PdfPanelAnnotationInteraction(pages, renderer.InputProcessor);
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
    /// Settings of the panel, applied by <see cref="Synchronize"/>.
    /// </summary>
    public PdfPanelSettings Settings { get; }

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
    /// Current pointer position in panel coordinates, or null if pointer is not over the panel.
    /// </summary>
    public PdfPoint? PointerPosition { get; set; }

    /// <summary>
    /// Current pointer button state.
    /// </summary>
    public PdfPanelButtonState PointerState { get; set; }

    /// <summary>
    /// The currently active annotation under the pointer, or null if no annotation is active.
    /// </summary>
    public PdfAnnotationPopup? ActiveAnnotation => _annotationInteraction.ActiveAnnotation;

    /// <summary>
    /// The interaction state of the active annotation.
    /// </summary>
    public PdfPanelPointerState ActiveAnnotationState => _annotationInteraction.ActiveAnnotationState;

    /// <summary>
    /// Annotation clicked during the last <see cref="Synchronize"/>, or null if none was clicked.
    /// </summary>
    public PdfAnnotationPopup? ClickedAnnotation => _annotationInteraction.ClickedAnnotation;

    /// <summary>
    /// Cursor shape the last pointer input resolved to.
    /// </summary>
    public PdfPanelCursor Cursor => _renderer.InputProcessor.Cursor;

    /// <summary>
    /// Collection of PDF pages to display.
    /// </summary>
    public PdfPanelPageCollection Pages { get; }

    /// <summary>
    /// Whether the text of every page is extracted.
    /// </summary>
    public bool ExtractText { get; set; }

    /// <summary>
    /// Text to search for in the document, or <see langword="null"/> when no search is active.
    /// </summary>
    public string? SearchQuery { get; set; }

    /// <summary>
    /// Gets the panel rectangle in scaled coordinate space.
    /// </summary>
    public PdfRectangle PanelRectangle => PdfRectangle.FromLocationAndSize(HorizontalOffset, VerticalOffset, PanelWidth, PanelHeight);

    /// <summary>
    /// Synchronizes the panel state with the current property values: recalculates dimensions and page positions,
    /// clamps scroll offsets and dispatches pointer input. Should be called after changing any property.
    /// </summary>
    public void Synchronize()
    {
        Scale = Clamp(Scale, Settings.Zoom.MinScale, Settings.Zoom.MaxScale);

        ApplyAutoScale();

        PdfSize extentSize = Settings.Layout.CalculateDimensions(Pages, Scale, PanelWidth, PanelHeight);

        ExtentWidth = extentSize.Width;
        ExtentHeight = extentSize.Height;

        Settings.Layout.CalculatePageOffsets(Pages, Scale, ExtentWidth, ExtentHeight);

        VerticalOffset = Clamp(VerticalOffset, 0, Math.Max(0, ExtentHeight - PanelHeight));
        HorizontalOffset = Clamp(HorizontalOffset, 0, Math.Max(0, ExtentWidth - PanelWidth));

        DispatchPointerInput();

        _renderer.Synchronize();
        _renderer.ContentProvider.UpdateTextExtraction(ExtractText);
        _renderer.UpdateSearch(SearchQuery);
    }

    /// <summary>
    /// Enqueues a rendering request for the visible pages to the rendering queue.
    /// </summary>
    public void Render()
    {
        _renderer.Submit(BuildRequest());
        _renderer.Submit(BuildUserInterfaceRequest());
    }

    /// <summary>
    /// Requests rendering without redrawing surface content to trigger <see cref="IPdfPanelRenderTarget.Render"/>.
    /// </summary>
    public void Refresh() => _renderer.Refresh();

    /// <summary>
    /// Resets visual state, cleans up rendering surface.
    /// </summary>
    public void Reset() => _renderer.Reset();

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

    private T GetBaseRequest<T>() where T : DrawingRequest, new()
    {
        return new()
        {
            Scale = Scale,
            ActiveAnnotation = ActiveAnnotation,
            ActiveAnnotationState = ActiveAnnotationState,
            Offset = new PdfPoint(HorizontalOffset, VerticalOffset),
            PanelSize = new PdfSize(PanelWidth, PanelHeight),
            RenderTarget = _renderTargetFactory.GetRenderTarget(this),
            VisiblePages = GetVisiblePages().ToArray()
        };
    }

    private PagesDrawingRequest BuildRequest()
    {
        PagesDrawingRequest request = GetBaseRequest<PagesDrawingRequest>();

        request.Rendering = Settings.Rendering.Clone();
        request.Appearance = Settings.Appearance.Clone();

        return request;
    }

    private UserInterfaceDrawingRequest BuildUserInterfaceRequest()
    {
        UserInterfaceDrawingRequest request = GetBaseRequest<UserInterfaceDrawingRequest>();

        request.PointerPosition = _renderer.InputProcessor.PointerPosition;

        return request;
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
                    Settings.Rendering.TileSize);
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
            float padding = Settings.Layout.Padding.Left + Settings.Layout.Padding.Right;
            fitScale = (PanelWidth - padding) / maxPageWidth;
        }
        else
        {
            fitScale = (PanelHeight - Settings.Layout.PageGap) / maxPageHeight;
        }

        float scale = Clamp(fitScale, Settings.Zoom.MinScale, Settings.Zoom.MaxScale);

        if (Math.Abs(scale - Scale) / Scale <= ScaleTolerance)
        {
            return;
        }

        this.UpdateScalePreserveOffset(scale, 0, 0);
    }

    private void DispatchPointerInput()
    {
        PdfPanelInputProcessor processor = _renderer.InputProcessor;

        _annotationInteraction.ClearClicked();

        if (PointerPosition == null)
        {
            processor.Leave();
            return;
        }

        processor.Update(ResolvePointerPosition(PointerPosition.Value), PointerState);
    }

    private static float Clamp(float value, float min, float max)
        => Math.Max(min, Math.Min(max, value));

    /// <inheritdoc />
    public void Dispose() => _annotationInteraction.Dispose();
}
