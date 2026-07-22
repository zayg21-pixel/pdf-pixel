namespace PdfPixel.Fonts.Model;

/// <summary>
/// Font metrics and style information, expressed in terms found in real font formats
/// (TrueType/OpenType OS/2, CFF, Type1) rather than PDF font-descriptor semantics.
/// </summary>
public sealed class PdfFontMetrics
{
    /// <summary>
    /// The font name (PostScript name or family name).
    /// </summary>
    public PdfFontString FontName { get; set; }

    /// <summary>
    /// Maximum height above baseline for glyphs in the font.
    /// </summary>
    public float Ascent { get; set; }

    /// <summary>
    /// Maximum depth below baseline for glyphs in the font.
    /// </summary>
    public float Descent { get; set; }

    /// <summary>
    /// Height of uppercase glyphs.
    /// </summary>
    public float CapHeight { get; set; }

    /// <summary>
    /// Height of lowercase x glyph.
    /// </summary>
    public float XHeight { get; set; }

    /// <summary>
    /// Italic angle of the font, in degrees counter-clockwise from vertical.
    /// </summary>
    public float ItalicAngle { get; set; }

    /// <summary>
    /// Left edge of the font bounding box in glyph design units.
    /// </summary>
    public float BoundingBoxLeft { get; set; }

    /// <summary>
    /// Bottom edge of the font bounding box in glyph design units.
    /// </summary>
    public float BoundingBoxBottom { get; set; }

    /// <summary>
    /// Right edge of the font bounding box in glyph design units.
    /// </summary>
    public float BoundingBoxRight { get; set; }

    /// <summary>
    /// Top edge of the font bounding box in glyph design units.
    /// </summary>
    public float BoundingBoxTop { get; set; }

    /// <summary>
    /// Average glyph width.
    /// </summary>
    public float AvgWidth { get; set; }

    /// <summary>
    /// Font weight (100-900, matching OS/2 usWeightClass).
    /// </summary>
    public int Weight { get; set; }

    /// <summary>
    /// Whether the font should be rendered bold even without a matching glyph program.
    /// </summary>
    public bool IsForceBold { get; set; }

    /// <summary>
    /// Whether the glyphs are slanted (italicised).
    /// </summary>
    public bool IsItalic { get; set; }

    /// <summary>
    /// PANOSE classification bytes (10 bytes), or null if unknown.
    /// </summary>
    public byte[]? Panose { get; set; }
}
