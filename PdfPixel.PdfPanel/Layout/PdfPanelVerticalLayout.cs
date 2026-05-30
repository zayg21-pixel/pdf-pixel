using PdfPixel.PdfPanel.Extensions;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Layout;

/// <summary>
/// Arranges pages in a single vertical column, centred horizontally within the viewport.
/// </summary>
public class PdfPanelVerticalLayout : IPdfPanelLayout
{
    /// <inheritdoc />
    public SKSize CalculateDimensions(
        PdfPanelPageCollection pages,
        float scale,
        SKRect pagesPadding,
        float pageGap,
        float viewportWidth,
        float viewportHeight)
    {
        if (pages == null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        int pageCount = pages.Count;

        float paddingLeft = pagesPadding.Left;
        float paddingRight = pagesPadding.Right;
        float paddingTop = pagesPadding.Top;
        float paddingBottom = pagesPadding.Bottom;
        float scaledPageGap = pageGap * scale;

        float maxPageWidthScaled = 0f;
        float totalHeightScaled = 0f;

        for (int i = 0; i < pageCount; i++)
        {
            PdfPanelPage page = pages[i];
            SKSize rotatedScaledSize = page.GetRotatedScaledSize(scale);

            maxPageWidthScaled = Math.Max(maxPageWidthScaled, rotatedScaledSize.Width);
            totalHeightScaled += rotatedScaledSize.Height;
        }

        if (pageCount > 1)
        {
            totalHeightScaled += scaledPageGap * (pageCount - 1);
        }

        float contentWidth = maxPageWidthScaled + paddingLeft + paddingRight;
        float extentWidth = Math.Max(viewportWidth, contentWidth);
        float extentHeight = totalHeightScaled + paddingTop + paddingBottom;

        return new SKSize(extentWidth, extentHeight);
    }

    /// <inheritdoc />
    public void CalculatePageOffsets(
        PdfPanelPageCollection pages,
        float scale,
        SKRect pagesPadding,
        float pageGap,
        float extentWidth,
        float extentHeight)
    {
        if (pages == null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        int pageCount = pages.Count;
        float paddingTop = pagesPadding.Top;
        float scaledPageGap = pageGap * scale;
        float verticalOffset = paddingTop;

        for (int i = 0; i < pageCount; i++)
        {
            PdfPanelPage page = pages[i];
            SKSize rotatedScaledSize = page.GetRotatedScaledSize(scale);

            float pageOffsetLeft = (extentWidth - rotatedScaledSize.Width) / 2f;

            page.Offset = new SKPoint(pageOffsetLeft, verticalOffset);

            verticalOffset += rotatedScaledSize.Height + scaledPageGap;
        }
    }
}

