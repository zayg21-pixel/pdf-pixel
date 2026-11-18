using PdfPixel.Imaging.Processing.Pipeline;

namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// Creates the host's pixels of page thumbnails. Called on the UI thread.
/// </summary>
public interface IPdfPanelThumbnailResourceFactory
{
    /// <summary>
    /// Creates the pixels of a thumbnail from <paramref name="samples"/>: opaque 8-bit RGB, valid only during the call.
    /// </summary>
    /// <param name="samples">The pixels of the thumbnail.</param>
    IPdfPanelThumbnailResource Create(PdfImageSamples samples);
}
