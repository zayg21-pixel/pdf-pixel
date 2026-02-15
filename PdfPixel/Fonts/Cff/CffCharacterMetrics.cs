namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Character metrics extracted from a CFF Type2 charstring.
/// </summary>
internal sealed class CffCharacterMetrics
{
    /// <summary>
    /// Horizontal advance width (actual width = nominalWidthX + width from charstring).
    /// Null if not explicitly specified in charstring (use defaultWidthX).
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// Left sidebearing (LSB) - horizontal distance from origin to left edge of glyph.
    /// </summary>
    public double? LeftSideBearing { get; set; }
}
