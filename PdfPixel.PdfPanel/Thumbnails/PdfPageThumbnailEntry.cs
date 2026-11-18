namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// The thumbnail of one page in <see cref="PdfPanelThumbnails"/>: whether it is requested, whether it is being
/// produced, and the host's pixels of it once it is.
/// </summary>
internal sealed class PdfPageThumbnailEntry
{
    /// <summary>
    /// Gets or sets whether the thumbnail is requested.
    /// </summary>
    public bool IsRequested { get; set; }

    /// <summary>
    /// Gets or sets whether the thumbnail is being produced.
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// Gets or sets the host's pixels of the thumbnail, or <see langword="null"/> while it is not produced.
    /// </summary>
    public IPdfPanelThumbnailResource? Resource { get; set; }
}
