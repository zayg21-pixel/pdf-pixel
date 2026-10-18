using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Kinds of content extracted from pages in the background.
/// </summary>
[Flags]
internal enum ExtractedContentType
{
    /// <summary>
    /// Extracted words of a page.
    /// </summary>
    Words = 1 << 0,

    /// <summary>
    /// Thumbnail of a page.
    /// </summary>
    Thumbnails = 1 << 1,

    /// <summary>
    /// Every kind of extracted content.
    /// </summary>
    All = Words | Thumbnails
}
