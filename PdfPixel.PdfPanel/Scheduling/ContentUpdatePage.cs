using PdfPixel.Geometry;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Scheduling;

/// <summary>
/// A page of a <see cref="ContentUpdateRequest"/>.
/// </summary>
internal sealed class ContentUpdatePage
{
    /// <summary>
    /// Initializes the page of a request.
    /// </summary>
    public ContentUpdatePage(PdfPanelPage page, bool isVisible, IReadOnlyList<PdfIntegerPoint> gridPoints)
    {
        Page = page;
        IsVisible = isVisible;
        GridPoints = gridPoints;
    }

    /// <summary>
    /// The page.
    /// </summary>
    public PdfPanelPage Page { get; }

    /// <summary>
    /// Whether the page is visible in the panel; otherwise it is ahead of the visible pages in the scroll direction.
    /// </summary>
    public bool IsVisible { get; }

    /// <summary>
    /// Columns and rows of the tiles of the page to rasterize, on the tile grid of the request's scale and tile size,
    /// in the order they are rasterized.
    /// </summary>
    public IReadOnlyList<PdfIntegerPoint> GridPoints { get; }
}
