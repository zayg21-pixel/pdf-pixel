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
}
