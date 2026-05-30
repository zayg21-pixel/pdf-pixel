namespace PdfPixel.Pattern.Model;

/// <summary>
/// Strongly typed paint type for tiling patterns (PDF spec Table 90)
/// </summary>
public enum PdfTilingPaintType
{
    /// <summary>
    /// No paint type specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Colored tiling pattern; the pattern cell includes its own color information.
    /// </summary>
    Colored = 1,

    /// <summary>
    /// Uncolored tiling pattern; color is supplied separately when the pattern is applied.
    /// </summary>
    Uncolored = 2
}
