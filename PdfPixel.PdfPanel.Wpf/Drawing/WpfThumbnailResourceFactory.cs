using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Thumbnails;

namespace PdfPixel.PdfPanel.Wpf.Drawing;

/// <summary>
/// Creates the pixels of page thumbnails as frozen RGB bitmaps.
/// </summary>
internal sealed class WpfThumbnailResourceFactory : IPdfPanelThumbnailResourceFactory
{
    /// <inheritdoc />
    public IPdfPanelThumbnailResource Create(PdfImageSamples samples) => new WpfPdfPanelThumbnailResource(samples);
}
