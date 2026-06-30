using PdfPixel.Commands;
using PdfPixel.TextExtraction;
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

    /// <summary>
    /// Combined features of all commands in the main content picture.
    /// </summary>
    public PdfCommandFeatures ContentFeatures { get; set; }

    /// <summary>
    /// Root of the text block tree extracted from the main content.
    /// </summary>
    public PdfTextBlock? ContentRootTextBlock { get; set; }
}
