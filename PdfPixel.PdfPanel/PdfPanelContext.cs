using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Layout;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Manages the viewport, layout, and rendering state for a PDF panel viewer.
/// </summary>
public class PdfPanelContext
{
    private readonly PdfRenderingQueue _pdfRenderingQueue;
    private readonly IPdfPanelRenderTargetFactory _renderTargetFactory;
    private readonly IPdfPanelLayout _layout;

    public PdfPanelContext(PdfPanelPageCollection pages, PdfRenderingQueue renderingQueue, IPdfPanelRenderTargetFactory renderTargetFactory, IPdfPanelLayout layout)
    {
        Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _pdfRenderingQueue = renderingQueue ?? throw new ArgumentNullException(nameof(renderingQueue));
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
    /// Padding from the edges of the viewing area to the pages, in unscaled page space.
    /// The effective on-screen padding is affected by <see cref="Scale"/>.
    /// </summary>
    public SKRect PagesPadding { get; set; } = SKRect.Create(10, 10, 10, 10);

    /// <summary>
    /// Gets or sets the spacing between pages in the layout, in unscaled page space.
    /// The effective on-screen gap is affected by <see cref="Scale"/>.
    /// </summary>
    public float MinimumPageGap { get; set; } = 10;

    /// <summary>
    /// Background color drawn behind the pages.
    /// </summary>
    public SKColor BackgroundColor { get; set; } = SKColors.LightGray;

    /// <summary>
    /// Maximum size in pixels for thumbnail generation.
    /// </summary>
    public int MaxThumbnailSize { get; set; } = 400;

    /// <summary>
    /// Collection of PDF pages to display.
    /// </summary>
    public PdfPanelPageCollection Pages { get; }

    /// <summary>
    /// Layout strategy used to position pages within the viewport.
    /// </summary>
    public IPdfPanelLayout Layout => _layout;

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
            Pages,
            Scale,
            PagesPadding,
            MinimumPageGap,
            ViewportWidth,
            ViewportHeight);

        ExtentWidth = extentSize.Width;
        ExtentHeight = extentSize.Height;

        _layout.CalculatePageOffsets(
            Pages,
            Scale,
            PagesPadding,
            MinimumPageGap,
            ExtentWidth,
            ExtentHeight);

        float scrollHeight = Math.Max(0, ExtentHeight - ViewportHeight);
        VerticalOffset = Clamp(VerticalOffset, 0, scrollHeight);

        float scrollWidth = Math.Max(0, ExtentWidth - ViewportWidth);
        HorizontalOffset = Clamp(HorizontalOffset, 0, scrollWidth);
    }

    /// <summary>
    /// Enqueues a rendering request for the visible pages to the rendering queue.
    /// </summary>
    public void Render()
    {
        var request = GetPagesDrawingRequest();
        _pdfRenderingQueue.EnqueueDrawingRequest(request);
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
                SKPoint offset = new SKPoint(offsetX, offsetY);
                yield return new VisiblePageInfo(i + 1, offset, page.Info, page.UserRotation);
            }
        }
    }

    private PagesDrawingRequest GetPagesDrawingRequest()
    {
        if (Pages == null)
        {
            return null;
        }

        var drawingRequest = GetBaseDrawingRequest<PagesDrawingRequest>();
        drawingRequest.Pages = Pages;
        drawingRequest.VisiblePages = GetVisiblePages().ToArray();
        drawingRequest.BackgroundColor = BackgroundColor;
        drawingRequest.MaxThumbnailSize = MaxThumbnailSize;

        return drawingRequest;
    }

    private T GetBaseDrawingRequest<T>() where T : DrawingRequest, new()
    {
        return new T
        {
            Scale = Scale,
            Offset = new SKPoint(HorizontalOffset, VerticalOffset),
            CanvasSize = new SKSize(ViewportWidth, ViewportHeight),
            RenderTarget = _renderTargetFactory.GetRenderTarget(this),
        };
    }

    private static float Clamp(float value, float min, float max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
