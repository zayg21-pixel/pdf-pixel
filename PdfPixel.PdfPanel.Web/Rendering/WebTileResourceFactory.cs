using PdfPixel.PdfPanel.Rendering;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// Creates the pixels of content tiles as canvases in the JS canvas store.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class WebTileResourceFactory : IPdfPanelTileResourceFactory
{
    /// <inheritdoc />
    public IPdfPanelTileResource Create(int tileSize) => new WebTileResource(tileSize);
}
