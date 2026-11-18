namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// Pages whose thumbnails the host requests and draws in place of pages that have no content tiles yet.
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
    VisiblePages
}
