using PdfPixel.Geometry;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// A content tile of a page to draw, from <see cref="PdfPanelTiles.GetTiles"/>.
/// </summary>
public readonly struct PdfPanelTile
{
    internal PdfPanelTile(IPdfPanelTileResource resource, in PdfRectangle destination)
    {
        Resource = resource;
        Destination = destination;
    }

    /// <summary>
    /// Gets the host's pixels of the tile.
    /// </summary>
    public IPdfPanelTileResource Resource { get; }

    /// <summary>
    /// Gets where the whole tile is drawn, in page content coordinates (top-left origin, unrotated), snapped so that
    /// its edges fall on whole panel pixels; it reaches past the page at its edges and is cut by the page clip.
    /// </summary>
    public PdfRectangle Destination { get; }
}
