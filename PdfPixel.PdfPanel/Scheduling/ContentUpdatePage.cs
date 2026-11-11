using PdfPixel.Geometry;

namespace PdfPixel.PdfPanel.Scheduling;

/// <summary>
/// A page of a <see cref="ContentUpdateRequest"/>.
/// </summary>
internal sealed class ContentUpdatePage
{
    /// <summary>
    /// Initializes the page of a request.
    /// </summary>
    public ContentUpdatePage(PdfPanelPage page, bool isVisible, in PdfRectangle visibleRegion)
    {
        Page = page;
        IsVisible = isVisible;
        VisibleRegion = visibleRegion;
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
    /// Part of the page whose tiles are kept, in page space snapped to the tile grid: tiles outside it are evicted and
    /// missing tiles inside it are rasterized.
    /// </summary>
    public PdfRectangle VisibleRegion { get; }
}
