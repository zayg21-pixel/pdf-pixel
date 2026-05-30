namespace PdfPixel.Pattern.Model;

/// <summary>
/// Identifies the PDF pattern type (/PatternType dictionary entry).
/// </summary>
public enum PdfPatternType
{
    /// <summary>
    /// No pattern or unknown type.
    /// </summary>
    None = 0,

    /// <summary>
    /// Tiling pattern: repeating cell painted across a region.
    /// </summary>
    Tiling = 1,

    /// <summary>
    /// Shading pattern: smooth gradient fill without a repeating cell.
    /// </summary>
    Shading = 2
}
