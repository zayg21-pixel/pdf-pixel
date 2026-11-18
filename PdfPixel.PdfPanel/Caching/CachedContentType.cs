using System;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// Kinds of content cached for a page.
/// </summary>
[Flags]
internal enum CachedContentType
{
    /// <summary>
    /// Command recordings of the page content and annotations.
    /// </summary>
    Content = 1 << 0,

    /// <summary>
    /// Extracted words of the page.
    /// </summary>
    Words = 1 << 1,

    /// <summary>
    /// Every kind of cached content.
    /// </summary>
    All = Content | Words
}
