namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Order of the 8-bit channels of each pixel of a content tile; every tile is opaque, its alpha always 255.
/// </summary>
public enum PdfPanelTileChannelOrder
{
    /// <summary>
    /// Red, green, blue, then alpha.
    /// </summary>
    Rgba,

    /// <summary>
    /// Blue, green, red, then alpha.
    /// </summary>
    Bgra
}
