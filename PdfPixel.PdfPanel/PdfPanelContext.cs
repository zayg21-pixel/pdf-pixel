using PdfPixel.Annotations.Models;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Layout;
using SkiaSharp;
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
public class PdfPanelContext
{
    private readonly PdfPanelRenderer _renderer;
    private readonly IPdfPanelRenderTargetFactory _renderTargetFactory;
    private readonly IPdfPanelLayout _layout;

    /// <summary>
    /// Initializes the context with the given page collection, renderer, render target factory, and layout.
    /// </summary>
    public PdfPanelContext(PdfPanelPageCollection pages, PdfPanelRenderer renderer, IPdfPanelRenderTargetFactory renderTargetFactory, IPdfPanelLayout layout)
    {
        Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _renderTargetFactory = renderTargetFactory ?? throw new ArgumentNullException(nameof(renderTargetFactory));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
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
    /// Rendering parameters used for PDF rendering.
    /// These parameters are passed to the rendering queue and can be used to customize rendering behavior, such as enabling debug overlays or adjusting rendering quality.
    /// </summary>
    public PdfRenderingParameters PdfRenderingParameters { get; } = new();

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
    public SKRect PagesPadding { get; set; } = SKRect.Create(10, 10, 10, 10);

    /// <summary>
    /// Gets or sets the spacing between pages in the layout, in unscaled page space.
    /// The effective on-screen gap is affected by <see cref="Scale"/>.
    /// </summary>
    public float MinimumPageGap { get; set; } = 10;

    /// <summary>
    /// Corner radius for page rendering in unscaled page space.
    /// A value of 0 renders pages with sharp corners. The effective on-screen radius is affected by <see cref="Scale"/>.
    /// </summary>
    public float PageCornerRadius { get; set; }

    /// <summary>
    /// Background color drawn behind the pages.
    /// </summary>
    public SKColor BackgroundColor { get; set; } = SKColors.LightGray;

    /// <summary>
    /// Current pointer position in viewport coordinates, or null if pointer is not over the panel.
    /// </summary>
    public SKPoint? PointerPosition { get; set; }

    /// <summary>
    /// Current pointer button state.
    /// </summary>
    public PdfPanelButtonState PointerState { get; set; }

    /// <summary>
    /// The currently active annotation under the pointer, or null if no annotation is active.
    /// </summary>
    public PdfAnnotationPopup? ActiveAnnotation { get; private set; }

    /// <summary>
    /// The interaction state of the active annotation.
    /// </summary>
    public PdfPanelPointerState ActiveAnnotationState { get; private set; }

    /// <summary>
    /// Collection of PDF pages to display.
    /// </summary>
    public PdfPanelPageCollection Pages { get; }

    /// <summary>
    /// Gets the viewport rectangle in scaled coordinate space.
    /// </summary>
    public SKRect ViewportRectangle => SKRect.Create(HorizontalOffset, VerticalOffset, ViewportWidth, ViewportHeight);

    /// <summary>
    /// Updates the layout by recalculating dimensions, page positions, and clamping scroll offsets.
    /// Should be called after changing viewport size, scale, or any layout properties.
    /// </summary>
    public void Update()
    {
        Scale = Clamp(Scale, MinScale, MaxScale);

        SKSize extentSize = _layout.CalculateDimensions(
            Pages, Scale, PagesPadding, MinimumPageGap, ViewportWidth, ViewportHeight);

        ExtentWidth = extentSize.Width;
        ExtentHeight = extentSize.Height;

        _layout.CalculatePageOffsets(
            Pages, Scale, PagesPadding, MinimumPageGap, ExtentWidth, ExtentHeight);

        VerticalOffset = Clamp(VerticalOffset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        HorizontalOffset = Clamp(HorizontalOffset, 0, Math.Max(0, ExtentWidth - ViewportWidth));

        UpdateActiveAnnotation();
    }

    /// <summary>
    /// Enqueues a rendering request for the visible pages to the rendering queue.
    /// </summary>
    public void Render()
    {
        PagesDrawingRequest? request = BuildRequest();
        if (request != null)
        {
            _renderer.Submit(request);
        }
    }

    /// <summary>
    /// Requests rendering without redrawing surface content to trigger <see cref="IPdfPanelRenderTarget.Render"/>.
    /// </summary>
    public void Refresh() => _renderer.Refresh();

    /// <summary>
    /// Resets visual state, cleans up rendering surface.
    /// </summary>
    public void Reset() => _renderer.Reset();

    private PagesDrawingRequest? BuildRequest()
    {
        if (Pages == null)
        {
            return null;
        }

        PdfRenderingParameters parameters = PdfRenderingParameters.Clone();
        parameters.ScaleFactor = Scale;

        return new PagesDrawingRequest
        {
            Scale = Scale,
            ActiveAnnotation = ActiveAnnotation,
            ActiveAnnotationState = ActiveAnnotationState,
            Offset = new SKPoint(HorizontalOffset, VerticalOffset),
            CanvasSize = new SKSize(ViewportWidth, ViewportHeight),
            RenderTarget = _renderTargetFactory.GetRenderTarget(this),
            VisiblePages = GetVisiblePages().ToArray(),
            BackgroundColor = BackgroundColor,
            PageCornerRadius = PageCornerRadius,
            RenderingParameters = parameters
        };
    }

    private IEnumerable<VisiblePageInfo> GetVisiblePages()
    {
        for (int i = 0; i < Pages.Count; i++)
        {
            PdfPanelPage page = Pages[i];

            if (page.IsPageVisible(ViewportRectangle, Scale))
            {
                float offsetX = (page.Offset.X - HorizontalOffset) / Scale;
                float offsetY = (page.Offset.Y - VerticalOffset) / Scale;
                yield return new VisiblePageInfo(i + 1, new SKPoint(offsetX, offsetY), page.Info, page.UserRotation);
            }
        }
    }

    private static float Clamp(float value, float min, float max)
        => Math.Max(min, Math.Min(max, value));

    private void UpdateActiveAnnotation()
    {
        PdfAnnotationPopup? newActiveAnnotation = null;
        var newState = PdfPanelPointerState.None;

        if (PointerPosition.HasValue && Pages != null)
        {
            for (int i = 0; i < Pages.Count; i++)
            {
                PdfPanelPage page = Pages[i];

                if (!page.IsPageVisible(ViewportRectangle, Scale))
                {
                    continue;
                }

                SKMatrix matrix = page.ViewportToPageMatrix(Scale, HorizontalOffset, VerticalOffset);
                SKPoint pagePoint = matrix.MapPoint(PointerPosition.Value);

                if (!page.IsPointInPageBounds(pagePoint))
                {
                    continue;
                }

                newActiveAnnotation = Pages.GetAnnotationPopupAt(i + 1, pagePoint);

                if (newActiveAnnotation != null)
                {
                    newState = (PointerState == PdfPanelButtonState.Pressed)
                        ? PdfPanelPointerState.Pressed
                        : PdfPanelPointerState.Hovered;
                    break;
                }
            }
        }

        ActiveAnnotation = newActiveAnnotation;
        ActiveAnnotationState = newState;
    }
}
