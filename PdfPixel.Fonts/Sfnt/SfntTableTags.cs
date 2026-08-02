namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Well-known SFNT table tags used by both TrueType- and CFF-flavored OpenType fonts.
/// </summary>
public static class SfntTableTags
{
    /// <summary>
    /// Font header: unitsPerEm, bounding box, index-to-loc format.
    /// </summary>
    public static readonly SfntTableTag Head = SfntTableTag.FromString("head");

    /// <summary>
    /// Horizontal header: ascent, descent, number of long horizontal metrics.
    /// </summary>
    public static readonly SfntTableTag Hhea = SfntTableTag.FromString("hhea");

    /// <summary>
    /// Horizontal metrics: per-glyph advance width and left side bearing.
    /// </summary>
    public static readonly SfntTableTag Hmtx = SfntTableTag.FromString("hmtx");

    /// <summary>
    /// Maximum profile: glyph count and (for TrueType) outline complexity limits.
    /// </summary>
    public static readonly SfntTableTag Maxp = SfntTableTag.FromString("maxp");

    /// <summary>
    /// Character-to-glyph mapping.
    /// </summary>
    public static readonly SfntTableTag Cmap = SfntTableTag.FromString("cmap");

    /// <summary>
    /// PostScript glyph names and metadata.
    /// </summary>
    public static readonly SfntTableTag Post = SfntTableTag.FromString("post");

    /// <summary>
    /// Naming table: font family, subfamily, and other human-readable strings.
    /// </summary>
    public static readonly SfntTableTag Name = SfntTableTag.FromString("name");

    /// <summary>
    /// OS/2 and Windows metrics.
    /// </summary>
    public static readonly SfntTableTag Os2 = SfntTableTag.FromString("OS/2");

    /// <summary>
    /// Compact Font Format outline data (CFF-flavored OpenType).
    /// </summary>
    public static readonly SfntTableTag Cff = SfntTableTag.FromString("CFF ");

    /// <summary>
    /// TrueType glyph outline data.
    /// </summary>
    public static readonly SfntTableTag Glyf = SfntTableTag.FromString("glyf");

    /// <summary>
    /// TrueType per-glyph offsets into <see cref="Glyf"/>.
    /// </summary>
    public static readonly SfntTableTag Loca = SfntTableTag.FromString("loca");

    /// <summary>
    /// Control value table read by the TrueType hinting instructions.
    /// </summary>
    public static readonly SfntTableTag Cvt = SfntTableTag.FromString("cvt ");

    /// <summary>
    /// Font program: TrueType instructions run once when the font is first loaded.
    /// </summary>
    public static readonly SfntTableTag Fpgm = SfntTableTag.FromString("fpgm");

    /// <summary>
    /// Control value program: TrueType instructions run whenever the point size changes.
    /// </summary>
    public static readonly SfntTableTag Prep = SfntTableTag.FromString("prep");

    /// <summary>
    /// Glyph definition table: glyph classes, attachment points, and ligature caret positions used by
    /// OpenType layout.
    /// </summary>
    public static readonly SfntTableTag Gdef = SfntTableTag.FromString("GDEF");

    /// <summary>
    /// Glyph substitution table: ligatures, alternates, and other OpenType layout substitutions.
    /// </summary>
    public static readonly SfntTableTag Gsub = SfntTableTag.FromString("GSUB");

    /// <summary>
    /// Glyph positioning table: kerning, mark attachment, and other OpenType layout adjustments.
    /// </summary>
    public static readonly SfntTableTag Gpos = SfntTableTag.FromString("GPOS");

    /// <summary>
    /// Grid-fitting and scan-conversion procedure: per-ppem-range hinting and smoothing policy.
    /// </summary>
    public static readonly SfntTableTag Gasp = SfntTableTag.FromString("gasp");
}
