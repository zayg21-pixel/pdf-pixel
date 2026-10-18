using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Data for <see cref="PdfPanelDocument.PageThumbnailRendered"/>.
/// </summary>
internal sealed class PageThumbnailRenderedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes the arguments for the given 1-based page number and PNG file.
    /// </summary>
    public PageThumbnailRenderedEventArgs(int pageNumber, byte[] png)
    {
        PageNumber = pageNumber;
        Png = png;
    }

    /// <summary>
    /// 1-based page number whose thumbnail was produced.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Thumbnail as a PNG file.
    /// </summary>
    public byte[] Png { get; }
}
