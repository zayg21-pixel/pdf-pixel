using System;

namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// Data for <see cref="PdfPanelText.PageTextExtracted"/>.
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
