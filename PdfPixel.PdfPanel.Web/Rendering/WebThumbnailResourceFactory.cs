using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Thumbnails;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// Creates the pixels of page thumbnails as canvases in the JS canvas store.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class WebThumbnailResourceFactory : IPdfPanelThumbnailResourceFactory
{
    /// <inheritdoc />
    public IPdfPanelThumbnailResource Create(PdfImageSamples samples) => new WebThumbnailResource(samples);
}
