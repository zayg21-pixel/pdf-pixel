using PdfPixel.PdfPanel.Rendering;

namespace PdfPixel.PdfPanel.Wpf.Drawing;

/// <summary>
/// Creates the pixels of content tiles as premultiplied BGRA bitmaps.
/// </summary>
internal sealed class WpfTileResourceFactory : IPdfPanelTileResourceFactory
{
    /// <inheritdoc />
    public IPdfPanelTileResource Create(int tileSize) => new WpfTileResource(tileSize);
}
