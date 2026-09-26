using PdfPixel.Geometry;

namespace PdfPixel.PdfPanel.Layout;

/// <summary>
/// Defines a layout strategy for positioning PDF pages within a viewport.
/// </summary>
public interface IPdfPanelLayout
{
    /// <summary>
    /// Padding from the edges of the viewing area to the pages, in device pixels.
    /// Unlike <see cref="PageGap"/>, this value is not scaled with the panel scale.
    /// </summary>
    PdfRectangle Padding { get; set; }

    /// <summary>
    /// Spacing between pages in unscaled page space.
    /// </summary>
    float PageGap { get; set; }

    /// <summary>
    /// Calculates the total extent (width and height) required to display all pages with the given layout.
    /// </summary>
    /// <param name="pages">The collection of pages to layout.</param>
    /// <param name="scale">The current zoom scale factor.</param>
    /// <param name="viewportWidth">The width of the viewport in device pixels.</param>
    /// <param name="viewportHeight">The height of the viewport in device pixels.</param>
    /// <returns>The total extent size in scaled space (device pixels).</returns>
    PdfSize CalculateDimensions(
        PdfPanelPageCollection pages,
        float scale,
        float viewportWidth,
        float viewportHeight);

    /// <summary>
    /// Calculates and assigns the position (Offset) for each page within the layout.
    /// Offsets are in scaled coordinate space.
    /// </summary>
    /// <param name="pages">The collection of pages to position.</param>
    /// <param name="scale">The current zoom scale factor.</param>
    /// <param name="extentWidth">The total width of the content area in scaled space.</param>
    /// <param name="extentHeight">The total height of the content area in scaled space.</param>
    void CalculatePageOffsets(
        PdfPanelPageCollection pages,
        float scale,
        float extentWidth,
        float extentHeight);
}


