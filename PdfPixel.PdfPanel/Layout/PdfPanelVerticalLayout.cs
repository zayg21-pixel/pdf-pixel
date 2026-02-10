using PdfPixel.PdfPanel.Extensions;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Layout;

public class PdfPanelVerticalLayout : IPdfPanelLayout
{
    public SKSize CalculateDimensions(
        PdfPanelPageCollection pages,
        float scale,
        SKRect pagesPadding,
        float pageGap,
        float viewportWidth,
        float viewportHeight)
    {
        int pageCount = pages.Count;

        float scaledPaddingLeft = pagesPadding.Left * scale;
        float scaledPaddingRight = pagesPadding.Width * scale;
        float scaledPaddingTop = pagesPadding.Top * scale;
        float scaledPaddingBottom = pagesPadding.Height * scale;
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

        float contentWidth = maxPageWidthScaled + scaledPaddingLeft + scaledPaddingRight;
        float extentWidth = Math.Max(viewportWidth, contentWidth);
        float extentHeight = totalHeightScaled + scaledPaddingTop + scaledPaddingBottom;

        return new SKSize(extentWidth, extentHeight);
    }

    public void CalculatePageOffsets(
        PdfPanelPageCollection pages,
        float scale,
        SKRect pagesPadding,
        float pageGap,
        float extentWidth,
        float extentHeight)
    {
        int pageCount = pages.Count;
        float scaledPaddingTop = pagesPadding.Top * scale;
        float scaledPageGap = pageGap * scale;
        float verticalOffset = scaledPaddingTop;

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

