namespace PdfPixel.Fonts.Model;

/// <summary>
/// Font metrics and style information, expressed in terms found in real font formats
/// (TrueType/OpenType OS/2, CFF, Type1) rather than PDF font-descriptor semantics.
/// <see cref="Ascent"/>, <see cref="Descent"/>, <see cref="CapHeight"/>, <see cref="XHeight"/>,
/// the bounding box, and <see cref="AvgWidth"/> are all pre-divided by the font's design units per
/// em square, so they are already em-relative (1.0 = one em) - callers never need to know or divide
/// by the font's own units-per-em to use them. <see cref="IPdfTypeface.GetWidth(ushort)"/> is the only
/// value on the typeface still in raw design units, since it is inherently per-glyph.
/// </summary>
public sealed class PdfFontMetrics
{
    /// <summary>
    /// The font name (PostScript name or family name).
    /// </summary>
    public PdfFontString FontName { get; set; }

    /// <summary>
    /// The font family name (typographic family, or the legacy family when the font has none).
    /// </summary>
    public PdfFontString FamilyName { get; set; }

    /// <summary>
    /// Design units per em square. Every other size on this type is already divided by this value;
    /// it is exposed only so callers can convert a raw <see cref="IPdfTypeface.GetWidth(ushort)"/>
    /// result (still in design units, being per-glyph) to the same em-relative units.
    /// </summary>
    public float UnitsPerEm { get; set; }

    /// <summary>
    /// Maximum height above baseline for glyphs in the font, in em-relative units.
    /// </summary>
    public float Ascent { get; set; }

    /// <summary>
    /// Maximum depth below baseline for glyphs in the font, in em-relative units.
    /// </summary>
    public float Descent { get; set; }

    /// <summary>
    /// Height of uppercase glyphs, in em-relative units.
    /// </summary>
    public float CapHeight { get; set; }

    /// <summary>
    /// Height of lowercase x glyph, in em-relative units.
    /// </summary>
    public float XHeight { get; set; }

    /// <summary>
    /// Italic angle of the font, in degrees counter-clockwise from vertical.
    /// </summary>
    public float ItalicAngle { get; set; }

    /// <summary>
    /// Left edge of the font bounding box, in em-relative units.
    /// </summary>
    public float BoundingBoxLeft { get; set; }

    /// <summary>
    /// Bottom edge of the font bounding box, in em-relative units.
    /// </summary>
    public float BoundingBoxBottom { get; set; }

    /// <summary>
    /// Right edge of the font bounding box, in em-relative units.
    /// </summary>
    public float BoundingBoxRight { get; set; }

    /// <summary>
    /// Top edge of the font bounding box, in em-relative units.
    /// </summary>
    public float BoundingBoxTop { get; set; }

    /// <summary>
    /// Average glyph width, in em-relative units.
    /// </summary>
    public float AvgWidth { get; set; }

    /// <summary>
    /// Font weight (100-900, matching OS/2 usWeightClass).
    /// </summary>
    public int Weight { get; set; }

    /// <summary>
    /// Font width (1-9, matching OS/2 usWidthClass).
    /// </summary>
    public int Width { get; set; }

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
