using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Annotations;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Manages the viewport, layout, and rendering state for a PDF panel viewer.
/// </summary>
public sealed class PdfPanelContext : IDisposable
{
    private readonly PdfPanelRenderer _renderer;
    private readonly IPdfPanelRenderTargetFactory _renderTargetFactory;
    private readonly PdfPanelAnnotationInteraction _annotationInteraction;
    private IPdfPanelLayout _layout = new PdfPanelVerticalLayout();
    private PdfPanelPointerPosition? _resolvedPointerPosition;
    private PdfPanelButtonState _lastPointerState;

    /// <summary>
    /// Initializes the context with the given page collection, renderer, and render target factory.
    /// </summary>
    public PdfPanelContext(PdfPanelPageCollection pages, PdfPanelRenderer renderer, IPdfPanelRenderTargetFactory renderTargetFactory)
    {
        Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _renderTargetFactory = renderTargetFactory ?? throw new ArgumentNullException(nameof(renderTargetFactory));

        _annotationInteraction = new PdfPanelAnnotationInteraction(pages, renderer.InputProcessor);
    }

    /// <summary>
    /// Width of the viewing area in device pixels (unscaled canvas space).
    /// </summary>
    public float ViewportWidth { get; set; }

    /// <summary>
    /// Height of the viewing area in device pixels (unscaled canvas space).
    /// </summary>
    public float ViewportHeight { get; set; }

    /// <summary>
    /// Parameters for PDF page rendering.
    /// </summary>
    public PdfRenderingParameters RenderingParameters { get; } = new() { CacheDecodedTiles = true };

    /// <summary>
    /// Parameters used for PDF command execution.
    /// </summary>
    public PdfCommandExecutionParameters CommandExecutionParameters { get; } = new();

    /// <summary>
    /// Total width of all pages including padding, in device pixels after applying <see cref="Scale"/>.
    /// </summary>
    public float ExtentWidth { get; private set; }

    /// <summary>
    /// Total height of all pages including padding, in device pixels after applying <see cref="Scale"/>.
    /// </summary>
    public float ExtentHeight { get; private set; }

    /// <summary>
    /// Vertical scroll offset in device pixels in the scaled canvas space.
    /// A value of 0 means the top of the content is aligned with the top of the viewport.
    /// </summary>
    public float VerticalOffset { get; set; }

    /// <summary>
    /// Horizontal scroll offset in device pixels in the scaled canvas space.
    /// A value of 0 means the left of the content is aligned with the left of the viewport.
    /// </summary>
    public float HorizontalOffset { get; set; }

    /// <summary>
    /// Current zoom factor. A value of 1.0 represents the natural size of the pages.
    /// Extent and offset values are expressed in the scaled space.
    /// </summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>
    /// Minimum allowed zoom scale factor.
    /// </summary>
    public float MinScale { get; set; } = 0.1f;

    /// <summary>
    /// Maximum allowed zoom scale factor.
    /// </summary>
    public float MaxScale { get; set; } = 10.0f;

    /// <summary>
    /// Padding from the edges of the viewing area to the pages, in device pixels.
    /// Unlike <see cref="MinimumPageGap"/>, this value is not scaled with <see cref="Scale"/>.
    /// </summary>
    public PdfRectangle PagesPadding { get; set; } = PdfRectangle.FromLocationAndSize(10, 10, 10, 10);

    /// <summary>
    /// Gets or sets the spacing between pages in the layout, in unscaled page space.
    /// The effective on-screen gap is affected by <see cref="Scale"/>.
    /// </summary>
    public float MinimumPageGap { get; set; } = 10;

    /// <summary>
    /// Current pointer position in viewport coordinates, or null if pointer is not over the panel.
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
    /// Annotation clicked during the last <see cref="Update"/>, or null if none was clicked.
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
    /// Layout that positions the pages within the viewport.
    /// </summary>
    public IPdfPanelLayout Layout
    {
        get => _layout;
        set => _layout = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the viewport rectangle in scaled coordinate space.
    /// </summary>
    public PdfRectangle ViewportRectangle => PdfRectangle.FromLocationAndSize(HorizontalOffset, VerticalOffset, ViewportWidth, ViewportHeight);

    /// <summary>
    /// Updates the layout by recalculating dimensions, page positions, and clamping scroll offsets.
    /// Should be called after changing viewport size, scale, or any layout properties.
    /// </summary>
    public void Update()
    {
        Scale = Clamp(Scale, MinScale, MaxScale);

        PdfSize extentSize = Layout.CalculateDimensions(
            Pages, Scale, PagesPadding, MinimumPageGap, ViewportWidth, ViewportHeight);

        ExtentWidth = extentSize.Width;
        ExtentHeight = extentSize.Height;

        Layout.CalculatePageOffsets(
            Pages, Scale, PagesPadding, MinimumPageGap, ExtentWidth, ExtentHeight);

        VerticalOffset = Clamp(VerticalOffset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        HorizontalOffset = Clamp(HorizontalOffset, 0, Math.Max(0, ExtentWidth - ViewportWidth));

        DispatchPointerInput();
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
    /// Maps a viewport position to the visible page it falls on.
    /// </summary>
    public PdfPanelPointerPosition ResolvePointerPosition(in PdfPoint viewportPosition)
    {
        for (int i = 0; i < Pages.Count; i++)
        {
            PdfPanelPage page = Pages[i];

            if (!page.IsPageVisible(ViewportRectangle, Scale))
            {
                continue;
            }

            PdfMatrix matrix = page.ViewportToPageMatrix(Scale, HorizontalOffset, VerticalOffset);
            PdfPoint pagePosition = matrix.MapPoint(viewportPosition);

            if (page.IsPointInPageBounds(pagePosition))
            {
                return new PdfPanelPointerPosition(viewportPosition, new PdfPanelPagePoint(i + 1, pagePosition));
            }
        }

        return new PdfPanelPointerPosition(viewportPosition, null);
    }

    private T GetBaseRequest<T>() where T : DrawingRequest, new()
    {
        return new()
        {
            Scale = Scale,
            ActiveAnnotation = ActiveAnnotation,
            ActiveAnnotationState = ActiveAnnotationState,
            Offset = new PdfPoint(HorizontalOffset, VerticalOffset),
            CanvasSize = new PdfSize(ViewportWidth, ViewportHeight),
            RenderTarget = _renderTargetFactory.GetRenderTarget(this),
            VisiblePages = GetVisiblePages().ToArray()
        };
    }

    private PagesDrawingRequest BuildRequest()
    {
        PagesDrawingRequest request = GetBaseRequest<PagesDrawingRequest>();

        request.ScaleFactor = Scale;
        request.CommandExecutionParameters = CommandExecutionParameters.Clone();
        request.RenderingParameters = RenderingParameters;

        return request;
    }

    private UserInterfaceDrawingRequest BuildUserInterfaceRequest()
    {
        UserInterfaceDrawingRequest request = GetBaseRequest<UserInterfaceDrawingRequest>();

        request.PointerPosition = _resolvedPointerPosition;

        return request;
    }

    private IEnumerable<VisiblePageInfo> GetVisiblePages()
    {
        PdfSize canvasSize = new(ViewportWidth, ViewportHeight);

        for (int i = 0; i < Pages.Count; i++)
        {
            PdfPanelPage page = Pages[i];

            if (page.IsPageVisible(ViewportRectangle, Scale))
            {
                float offsetX = (page.Offset.X - HorizontalOffset) / Scale;
                float offsetY = (page.Offset.Y - VerticalOffset) / Scale;
                yield return new VisiblePageInfo(
                    i + 1,
                    new PdfPoint(offsetX, offsetY),
                    page.Info,
                    page.UserRotation,
                    canvasSize,
                    Scale,
                    _renderer.Properties.TileSize);
            }
        }
    }

    private static float Clamp(float value, float min, float max)
        => Math.Max(min, Math.Min(max, value));

    private void DispatchPointerInput()
    {
        PdfPanelInputProcessor processor = _renderer.InputProcessor;

        _annotationInteraction.ClearClicked();

        if (PointerPosition == null)
        {
            if (_resolvedPointerPosition != null)
            {
                processor.Leave();
            }

            _resolvedPointerPosition = null;
            _lastPointerState = PdfPanelButtonState.Default;
            return;
        }

        PdfPanelPointerPosition position = ResolvePointerPosition(PointerPosition.Value);
        _resolvedPointerPosition = position;

        if (PointerState == PdfPanelButtonState.Pressed && _lastPointerState == PdfPanelButtonState.Default)
        {
            processor.Press(position);
        }
        else if (PointerState == PdfPanelButtonState.Default && _lastPointerState == PdfPanelButtonState.Pressed)
        {
            processor.Release(position);
        }
        else
        {
            processor.Move(position);
        }

        _lastPointerState = PointerState;
    }

    /// <inheritdoc />
    public void Dispose() => _annotationInteraction.Dispose();
}
