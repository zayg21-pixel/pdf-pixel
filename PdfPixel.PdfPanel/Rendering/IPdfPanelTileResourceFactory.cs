namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Creates the host's pixels of content tiles. Called on the UI thread.
/// </summary>
public interface IPdfPanelTileResourceFactory
{
    /// <summary>
    /// Creates the pixels of a tile of <paramref name="tileSize"/> by <paramref name="tileSize"/> pixels, their content
    /// undefined until they are written.
    /// </summary>
    /// <param name="tileSize">The edge length of the tile in pixels.</param>
    IPdfPanelTileResource Create(int tileSize);
}
