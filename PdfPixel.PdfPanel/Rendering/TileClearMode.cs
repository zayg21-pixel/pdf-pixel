namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Controls when existing tiles are cleared before rasterizing new ones.
/// </summary>
public enum TileClearMode
{
    /// <summary>
    /// Keep existing tiles, only rasterize missing ones.
    /// </summary>
    None,

    /// <summary>
    /// Clear existing tiles when the scale has changed since the last rasterization.
    /// </summary>
    ClearOnScaleChange,

    /// <summary>
    /// Unconditionally clear all existing tiles.
    /// </summary>
    ForceClear
}
