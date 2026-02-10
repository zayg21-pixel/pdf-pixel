using SkiaSharp;

namespace PdfPixel.PdfPanel.Layout;

/// <summary>
/// Defines a layout strategy for positioning PDF pages within a viewport.
/// </summary>
public interface IPdfPanelLayout
{
    /// <summary>
    /// Calculates the total extent (width and height) required to display all pages with the given layout.
    /// </summary>
    /// <param name="pages">The collection of pages to layout.</param>
    /// <param name="scale">The current zoom scale factor.</param>
    /// <param name="pagesPadding">The padding around pages in unscaled space. Left/Top represent the left/top padding, Width/Height represent the right/bottom padding.</param>
    /// <param name="pageGap">The spacing between pages in unscaled space.</param>
    /// <param name="viewportWidth">The width of the viewport in device pixels.</param>
    /// <param name="viewportHeight">The height of the viewport in device pixels.</param>
    /// <returns>The total extent size in scaled space (device pixels).</returns>
    SKSize CalculateDimensions(
        PdfPanelPageCollection pages,
        float scale,
        SKRect pagesPadding,
        float pageGap,
        float viewportWidth,
        float viewportHeight);

    /// <summary>
    /// Calculates and assigns the position (Offset) for each page within the layout.
    /// Offsets are in scaled coordinate space.
    /// </summary>
    /// <param name="pages">The collection of pages to position.</param>
    /// <param name="scale">The current zoom scale factor.</param>
    /// <param name="pagesPadding">The padding around pages in unscaled space. Left/Top represent the left/top padding, Width/Height represent the right/bottom padding.</param>
    /// <param name="pageGap">The spacing between pages in unscaled space.</param>
    /// <param name="extentWidth">The total width of the content area in scaled space.</param>
    /// <param name="extentHeight">The total height of the content area in scaled space.</param>
    void CalculatePageOffsets(
        PdfPanelPageCollection pages,
        float scale,
        SKRect pagesPadding,
        float pageGap,
        float extentWidth,
        float extentHeight);
}


