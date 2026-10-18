using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Kinds of content cached for a page.
/// </summary>
[Flags]
internal enum CachedContentType
{
    /// <summary>
    /// Command recording and picture of the page content.
    /// </summary>
    Content = 1 << 0,

    /// <summary>
    /// Command recording and picture of the page annotations.
    /// </summary>
    Annotations = 1 << 1,

    /// <summary>
    /// Extracted words of the page.
    /// </summary>
    Words = 1 << 2,

    /// <summary>
    /// Thumbnail of the page.
    /// </summary>
    Thumbnail = 1 << 3,

    /// <summary>
    /// Every kind of cached content.
    /// </summary>
    All = Content | Annotations | Words | Thumbnail
}
