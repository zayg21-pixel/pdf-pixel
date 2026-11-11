using System;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// Kinds of content cached for a page.
/// </summary>
[Flags]
internal enum CachedContentType
{
    /// <summary>
    /// Command recordings of the page content and annotations, and the tiles rasterized from them.
    /// </summary>
    Content = 1 << 0,

    /// <summary>
    /// Extracted words of the page.
    /// </summary>
    Words = 1 << 1,

    /// <summary>
    /// Thumbnail of the page.
    /// </summary>
    Thumbnail = 1 << 2,

    /// <summary>
    /// Every kind of cached content.
    /// </summary>
    All = Content | Words | Thumbnail
}
