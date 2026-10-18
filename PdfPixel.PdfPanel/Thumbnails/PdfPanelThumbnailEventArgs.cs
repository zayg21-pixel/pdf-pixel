using System;

namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// Data for <see cref="PdfPanelThumbnails.ThumbnailReady"/>.
/// </summary>
public sealed class PdfPanelThumbnailEventArgs : EventArgs
{
    /// <summary>
    /// Initializes the arguments for the given 1-based page number and PNG file.
    /// </summary>
    public PdfPanelThumbnailEventArgs(int pageNumber, byte[] png)
    {
        PageNumber = pageNumber;
        Png = png;
    }

    /// <summary>
    /// 1-based page number whose thumbnail is ready.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Thumbnail as a PNG file.
    /// </summary>
    public byte[] Png { get; }
}
