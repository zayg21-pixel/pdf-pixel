namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// Pages whose thumbnails are produced without being requested.
/// </summary>
public enum PdfPanelThumbnailPreloadMode
{
    /// <summary>
    /// Only requested thumbnails are produced.
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
