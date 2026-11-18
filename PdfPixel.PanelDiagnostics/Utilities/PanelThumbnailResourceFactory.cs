using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Thumbnails;

namespace PdfPixel.PanelDiagnostics.Utilities;

/// <summary>
/// Creates thumbnail resources that keep no pixels; the replay requests no thumbnails.
/// </summary>
internal sealed class PanelThumbnailResourceFactory : IPdfPanelThumbnailResourceFactory
{
    /// <inheritdoc />
    public IPdfPanelThumbnailResource Create(PdfImageSamples samples) => new PanelThumbnailResource();
}
