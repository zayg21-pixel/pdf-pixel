using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Extensions;
using System;

namespace PdfPixel.PdfPanel.Layout;

/// <summary>
/// Arranges pages in a single vertical column, centred horizontally within the viewport.
/// </summary>
public class PdfPanelVerticalLayout : IPdfPanelLayout
{
    /// <inheritdoc />
    public PdfRectangle Padding { get; set; } = new(10, 10, 10, 10);

    /// <inheritdoc />
    public float PageGap { get; set; } = 10;

    /// <inheritdoc />
    public PdfSize CalculateDimensions(
        PdfPanelPageCollection pages,
        float scale,
        float viewportWidth,
        float viewportHeight)
    {
        if (pages == null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        int pageCount = pages.Count;

        float paddingLeft = Padding.Left;
        float paddingRight = Padding.Right;
        float paddingTop = Padding.Top;
        float paddingBottom = Padding.Bottom;
        float scaledPageGap = PageGap * scale;

        float maxPageWidthScaled = 0f;
        float totalHeightScaled = 0f;

        for (int i = 0; i < pageCount; i++)
        {
            PdfPanelPage page = pages[i];
            PdfSize rotatedScaledSize = page.GetRotatedScaledSize(scale);

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

        return new PdfSize(extentWidth, extentHeight);
    }

    /// <inheritdoc />
    public void CalculatePageOffsets(
        PdfPanelPageCollection pages,
        float scale,
        float extentWidth,
        float extentHeight)
    {
        if (pages == null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        int pageCount = pages.Count;
        float paddingTop = Padding.Top;
        float scaledPageGap = PageGap * scale;
        float verticalOffset = paddingTop;

        for (int i = 0; i < pageCount; i++)
        {
            PdfPanelPage page = pages[i];
            PdfSize rotatedScaledSize = page.GetRotatedScaledSize(scale);

            float pageOffsetLeft = (extentWidth - rotatedScaledSize.Width) / 2f;

            page.Offset = new PdfPoint(pageOffsetLeft, verticalOffset);

            verticalOffset += rotatedScaledSize.Height + scaledPageGap;
        }
    }
}

