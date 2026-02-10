using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel;

public enum PageRenderTiling
{
    Vertical,
    TwoColumns
}

public class PdfPanelContext
{
    private readonly PdfRenderingQueue _pdfRenderingQueue;
    private IPdfPanelRenderTargetFactory _renderTargetFactory;

    public PdfPanelContext(PdfPanelPageCollection pages, PdfRenderingQueue renderingQueue, IPdfPanelRenderTargetFactory renderTargetFactory)
    {
        Pages = pages;
        _pdfRenderingQueue = renderingQueue;
        _renderTargetFactory = renderTargetFactory;
    }

    /// <summary>
    /// Width of the viewing area.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// Height of the viewing area.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    /// Total width of all pages including padding.
    /// </summary>
    public float ExtentWidth { get; private set; }

    /// <summary>
    /// Total height of all pages including padding.
    /// </summary>
    public float ExtentHeight { get; private set; }

    public int CurrentPage { get; private set; }

    public float VerticalOffset { get; set; }

    public float HorizontalOffset { get; set; }

    public float Scale { get; set; } = 1.0f;

    public float MinScale { get; set; } = 0.1f;

    public float MaxScale { get; set; } = 10.0f;

    /// <summary>
    /// Padding from the edges of the viewing area to the pages.
    /// </summary>
    public SKRect PagesPadding { get; set; } = SKRect.Create(10, 10, 10, 10);

    /// <summary>
    /// Gets or sets the spacing, between pages in the layout.
    /// </summary>
    public float MinimumPageGap { get; set; } = 10;

    public SKColor BackgroundColor { get; set; } = SKColors.LightGray;

    public int MaxThumbnailSize { get; set; } = 400;

    public PdfPanelPageCollection Pages { get; }

    private IEnumerable<VisiblePageInfo> GetVisiblePages()
    {
        if (Pages == null)
        {
            yield break;
        }

        float verticalOffset = -VerticalOffset / Scale + PagesPadding.Top;
        float horizontalOffset = -HorizontalOffset / Scale;

        var centerOffset = GetCenterOffset() / Scale;
        horizontalOffset += centerOffset;

        for (int i = 0; i < Pages.Count; i++)
        {
            var page = Pages[i];
            var rotatedSize = page.Info.GetRotatedSize(page.UserRotation);

            if (IsPageVisible(page))
            {
                var pageOffsetLeft = (ExtentWidth / Scale - rotatedSize.Width) / 2;
                var offset = new SKPoint(horizontalOffset + pageOffsetLeft, verticalOffset);
                yield return new VisiblePageInfo(i + 1, offset, page.Info, page.UserRotation);
            }

            verticalOffset += rotatedSize.Height + MinimumPageGap;
        }
    }

    public float GetCenterOffset()
    {
        var centerOffset = (Width - ExtentWidth) / 2;

        if (centerOffset < 0)
        {
            centerOffset = 0;
        }

        return centerOffset;
    }

    public bool IsPageVisible(PdfPanelPage page)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        float visibleTop = VerticalOffset;
        float visibleBottom = VerticalOffset + Height;

        SKSize rotatedSize = page.Info.GetRotatedSize(page.UserRotation);
        float pageTop = page.Offset.Y;
        float pageBottom = pageTop + rotatedSize.Height * Scale;

        bool topInside = pageTop >= visibleTop && pageTop <= visibleBottom;
        bool bottomInside = pageBottom >= visibleTop && pageBottom <= visibleBottom;
        bool coversViewport = pageTop <= visibleTop && pageBottom >= visibleBottom;

        return topInside || bottomInside || coversViewport;
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
            CanvasSize = new SKSize(Width, Height),
            RenderTarget = _renderTargetFactory.GetRenderTarget(this),
        };
    }


    public void Update()
    {
        var pageCount = Pages.Count;

        Scale = Clamp(Scale, MinScale, MaxScale);

        var maxPageWidth = 0f;
        var totalHeight = 0f;

        foreach (var page in Pages)
        {
            var rotatedSize = page.Info.GetRotatedSize(page.UserRotation);
            maxPageWidth = Math.Max(maxPageWidth, rotatedSize.Width);
            totalHeight += rotatedSize.Height;
        }

        if (pageCount > 1)
        {
            totalHeight += MinimumPageGap * (pageCount - 1);
        }

        ExtentWidth = (maxPageWidth + PagesPadding.Left + PagesPadding.Right) * Scale;
        ExtentHeight = (PagesPadding.Top + totalHeight + PagesPadding.Bottom) * Scale;

        float contentWidth = ExtentWidth;
        float verticalOffset = PagesPadding.Top * Scale;

        for (int i = 0; i < pageCount; i++)
        {
            PdfPanelPage page = Pages[i];
            SKSize rotatedSize = page.Info.GetRotatedSize(page.UserRotation);

            float pageWidthScaled = rotatedSize.Width * Scale;
            float pageHeightScaled = rotatedSize.Height * Scale;

            float pageOffsetLeft = (contentWidth - pageWidthScaled) / 2f;

            page.Offset = new SKPoint(pageOffsetLeft, verticalOffset);

            verticalOffset += pageHeightScaled + MinimumPageGap * Scale;
        }

        float currentPageVerticalOffset = -VerticalOffset;

        for (int i = 0; i < pageCount; i++)
        {
            PdfPanelPage page = Pages[i];

            float pageHeightScaled = page.Info.GetRotatedSize(page.UserRotation).Height * Scale;
            float pageTop = currentPageVerticalOffset;
            float pageBottom = currentPageVerticalOffset + pageHeightScaled + MinimumPageGap * Scale;

            if ((pageTop >= -MinimumPageGap * Scale && pageTop <= Height / 2) || (pageTop <= -MinimumPageGap * Scale && pageBottom >= Height / 2))
            {
                CurrentPage = page.PageNumber;
                break;
            }

            currentPageVerticalOffset += pageHeightScaled + MinimumPageGap * Scale;
        }

        var scrollHeight = Math.Max(0, ExtentHeight - Height);
        VerticalOffset = Clamp(VerticalOffset, 0, scrollHeight);

        var scrollWidth = Math.Max(0, ExtentWidth - Width);
        HorizontalOffset = Clamp(HorizontalOffset, 0, scrollWidth);
    }

    public static float Clamp(float value, float min, float max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    public void Render()
    {
        var request = GetPagesDrawingRequest();
        _pdfRenderingQueue.EnqueueDrawingRequest(request);
    }
}
