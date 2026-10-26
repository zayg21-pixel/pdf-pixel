using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Thumbnails;

namespace PdfPixel.PdfPanel.Scheduling;

/// <summary>
/// Settings that control how <see cref="PdfPanelScheduler"/> produces thumbnails.
/// </summary>
internal struct PdfPanelThumbnailSchedulingSettings
{
    /// <summary>
    /// Whether the page's thumbnail image is used when the page has one.
    /// </summary>
    public bool UsePageThumbnails { get; set; }

    /// <summary>
    /// Size in pixels a thumbnail is fitted within.
    /// </summary>
    public PdfIntegerSize RenderMaximumSize { get; set; }

    /// <summary>
    /// Maximum number of thumbnails kept, the ones nearest to the first visible page.
    /// Pinned thumbnails, and the thumbnails of visible pages while there are placeholders, are kept beyond it.
    /// </summary>
    public int MaximumCount { get; set; }

    /// <summary>
    /// Pages whose thumbnails are produced without being requested and drawn in place of pages that have no decoded content yet.
    /// </summary>
    public PdfPanelThumbnailPlaceholderMode PlaceholderMode { get; set; }
}
