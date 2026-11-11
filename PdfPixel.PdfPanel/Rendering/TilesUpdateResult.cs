namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// What a tiling pass did to the tiles of a page.
/// </summary>
internal enum TilesUpdateResult
{
    /// <summary>
    /// No tile needed rasterizing.
    /// </summary>
    Unchanged,

    /// <summary>
    /// Every tile of the visible region is done and tiles were rasterized.
    /// </summary>
    Updated,

    /// <summary>
    /// The pass was cancelled; the tiles it rasterized on the grid of the stored tiles are kept.
    /// </summary>
    Cancelled
}
