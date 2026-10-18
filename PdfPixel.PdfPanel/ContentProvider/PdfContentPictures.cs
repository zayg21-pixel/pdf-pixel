using SkiaSharp;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Cached pictures for a single page — main content and annotation layer recordings, and the thumbnail.
/// </summary>
internal sealed class PdfContentPictures
{
    /// <summary>
    /// Locked reference to the main page content picture, or <see langword="null"/> if not yet decoded.
    /// </summary>
    public ContentLocker<SKPicture>? Content { get; set; }

    /// <summary>
    /// Locked reference to the annotation layer picture, or <see langword="null"/> if not yet decoded.
    /// </summary>
    public ContentLocker<SKPicture>? Annotations { get; set; }

    /// <summary>
    /// Locked reference to the thumbnail as a PNG file, or <see langword="null"/> if not set.
    /// </summary>
    public ContentLocker<byte[]>? Thumbnail { get; set; }
}
