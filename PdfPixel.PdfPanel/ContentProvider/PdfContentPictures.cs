using SkiaSharp;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Pairs of cached <see cref="SKPicture"/> recordings for a single page — main content and annotation layer.
/// </summary>
public class PdfContentPictures
{
    /// <summary>
    /// Locked reference to the main page content picture, or <see langword="null"/> if not yet decoded.
    /// </summary>
    public ContentLocker<SKPicture>? Content { get; set; }

    /// <summary>
    /// Locked reference to the annotation layer picture, or <see langword="null"/> if not yet decoded.
    /// </summary>
    public ContentLocker<SKPicture>? Annotations { get; set; }
}
