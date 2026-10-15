using PdfPixel.PdfPanel.Execution;
using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Data for <see cref="PdfPageContentProvider.PageThumbnailRendered"/>.
/// </summary>
internal sealed class PageThumbnailRenderedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes the arguments for the given 1-based page number, request observer and PNG file.
    /// </summary>
    public PageThumbnailRenderedEventArgs(int pageNumber, IPdfCancellableExecutionObserver observer, byte[] png)
    {
        PageNumber = pageNumber;
        Observer = observer;
        Png = png;
    }

    /// <summary>
    /// 1-based page number whose thumbnail was produced.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Observer of the request that produced the thumbnail.
    /// </summary>
    public IPdfCancellableExecutionObserver Observer { get; }

    /// <summary>
    /// Thumbnail as a PNG file.
    /// </summary>
    public byte[] Png { get; }
}
