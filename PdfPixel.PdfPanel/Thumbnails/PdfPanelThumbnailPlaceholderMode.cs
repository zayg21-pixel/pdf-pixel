namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// Pages whose thumbnails are produced without being requested and drawn in place of pages that have no decoded content yet.
/// </summary>
public enum PdfPanelThumbnailPlaceholderMode
{
    /// <summary>
    /// No thumbnails are drawn in place of pages.
    /// </summary>
    None,

    /// <summary>
    /// Thumbnails of the visible pages.
    /// </summary>
    VisiblePages,

    /// <summary>
    /// Thumbnails of the pages nearest to the first visible page, up to <see cref="PdfPanelThumbnails.MaximumCount"/>.
    /// </summary>
    NearestPages
}
