using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Data for <see cref="IPdfPageContentProvider.PageTextExtracted"/>.
/// </summary>
public sealed class PageTextExtractedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes the arguments for the given 1-based page number.
    /// </summary>
    public PageTextExtractedEventArgs(int pageNumber) => PageNumber = pageNumber;

    /// <summary>
    /// 1-based page number whose characters were extracted.
    /// </summary>
    public int PageNumber { get; }
}
