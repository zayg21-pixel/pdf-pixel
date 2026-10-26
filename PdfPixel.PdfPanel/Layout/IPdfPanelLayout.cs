using PdfPixel.Geometry;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Layout;

/// <summary>
/// Defines a layout strategy for positioning PDF pages within a panel.
/// </summary>
public interface IPdfPanelLayout
{
    /// <summary>
    /// Padding from the edges of the viewing area to the pages, in device pixels.
    /// Unlike <see cref="PageGap"/>, this value is not scaled with the panel scale.
    /// </summary>
    PdfRectangle Padding { get; set; }

    /// <summary>
    /// Spacing between pages in unscaled panel space.
    /// </summary>
    float PageGap { get; set; }

    /// <summary>
    /// Calculates the size the panel's width or height is fitted to, in unscaled panel space: the widest and the
    /// tallest group of pages the layout shows side by side.
    /// </summary>
    /// <param name="pages">The collection of pages to layout.</param>
    /// <returns>The size to fit, in unscaled panel space.</returns>
    PdfSize CalculateFitSize(IReadOnlyList<PdfPanelPage> pages);

    /// <summary>
    /// Calculates the total extent (width and height) required to display all pages with the given layout.
    /// </summary>
    /// <param name="pages">The collection of pages to layout.</param>
    /// <param name="scale">The current zoom scale factor.</param>
    /// <param name="panelWidth">The width of the panel in device pixels.</param>
    /// <param name="panelHeight">The height of the panel in device pixels.</param>
    /// <returns>The total extent size in scaled space (device pixels).</returns>
    PdfSize CalculateDimensions(
        IReadOnlyList<PdfPanelPage> pages,
        float scale,
        float panelWidth,
        float panelHeight);

    /// <summary>
    /// Calculates and assigns the position (Offset) for each page within the layout.
    /// Offsets are in scaled coordinate space.
    /// </summary>
    /// <param name="pages">The collection of pages to position.</param>
    /// <param name="scale">The current zoom scale factor.</param>
    /// <param name="extentWidth">The total width of the content area in scaled space.</param>
    /// <param name="extentHeight">The total height of the content area in scaled space.</param>
    void CalculatePageOffsets(
        IReadOnlyList<PdfPanelPage> pages,
        float scale,
        float extentWidth,
        float extentHeight);
}


