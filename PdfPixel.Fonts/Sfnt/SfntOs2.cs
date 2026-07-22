namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "OS/2" table. Fields introduced in version 1 and later are null when the
/// table's version doesn't carry them.
/// </summary>
public class SfntOs2
{
    /// <summary>
    /// Gets or sets the table's version number (0 through 5).
    /// </summary>
    public ushort Version { get; set; }

    /// <summary>
    /// Gets or sets the arithmetic average of the advance widths of all non-zero-width glyphs.
    /// </summary>
    public short XAvgCharWidth { get; set; }

    /// <summary>
    /// Gets or sets the visual weight class (100-900, e.g. 400 = regular, 700 = bold).
    /// </summary>
    public ushort UsWeightClass { get; set; }

    /// <summary>
    /// Gets or sets the visual width class (1-9, e.g. 5 = medium/normal).
    /// </summary>
    public ushort UsWidthClass { get; set; }

    /// <summary>
    /// Gets or sets the embedding licensing rights bit field.
    /// </summary>
    public ushort FsType { get; set; }

    /// <summary>
    /// Gets or sets the subscript horizontal font size.
    /// </summary>
    public short YSubscriptXSize { get; set; }

    /// <summary>
    /// Gets or sets the subscript vertical font size.
    /// </summary>
    public short YSubscriptYSize { get; set; }

    /// <summary>
    /// Gets or sets the subscript x offset.
    /// </summary>
    public short YSubscriptXOffset { get; set; }

    /// <summary>
    /// Gets or sets the subscript y offset.
    /// </summary>
    public short YSubscriptYOffset { get; set; }

    /// <summary>
    /// Gets or sets the superscript horizontal font size.
    /// </summary>
    public short YSuperscriptXSize { get; set; }

    /// <summary>
    /// Gets or sets the superscript vertical font size.
    /// </summary>
    public short YSuperscriptYSize { get; set; }

    /// <summary>
    /// Gets or sets the superscript x offset.
    /// </summary>
    public short YSuperscriptXOffset { get; set; }

    /// <summary>
    /// Gets or sets the superscript y offset.
    /// </summary>
    public short YSuperscriptYOffset { get; set; }

    /// <summary>
    /// Gets or sets the strikeout size.
    /// </summary>
    public short YStrikeoutSize { get; set; }

    /// <summary>
    /// Gets or sets the strikeout position.
    /// </summary>
    public short YStrikeoutPosition { get; set; }

    /// <summary>
    /// Gets or sets the IBM font family class and subclass.
    /// </summary>
    public short SFamilyClass { get; set; }

    /// <summary>
    /// Gets or sets the 10-byte PANOSE classification number.
    /// </summary>
    public byte[] Panose { get; set; } = new byte[10];

    /// <summary>
    /// Gets or sets the first block of the Unicode range bit field (code points U+0000-U+7FFF).
    /// </summary>
    public uint UlUnicodeRange1 { get; set; }

    /// <summary>
    /// Gets or sets the second block of the Unicode range bit field.
    /// </summary>
    public uint UlUnicodeRange2 { get; set; }

    /// <summary>
    /// Gets or sets the third block of the Unicode range bit field.
    /// </summary>
    public uint UlUnicodeRange3 { get; set; }

    /// <summary>
    /// Gets or sets the fourth block of the Unicode range bit field.
    /// </summary>
    public uint UlUnicodeRange4 { get; set; }

    /// <summary>
    /// Gets or sets the font vendor's 4-character identification tag.
    /// </summary>
    public SfntTableTag AchVendId { get; set; }

    /// <summary>
    /// Gets or sets the font selection bit field (italic, bold, regular, ...).
    /// </summary>
    public ushort FsSelection { get; set; }

    /// <summary>
    /// Gets or sets the first Unicode code point applicable to this font.
    /// </summary>
    public ushort UsFirstCharIndex { get; set; }

    /// <summary>
    /// Gets or sets the last Unicode code point applicable to this font.
    /// </summary>
    public ushort UsLastCharIndex { get; set; }

    /// <summary>
    /// Gets or sets the typographic ascender.
    /// </summary>
    public short STypoAscender { get; set; }

    /// <summary>
    /// Gets or sets the typographic descender.
    /// </summary>
    public short STypoDescender { get; set; }

    /// <summary>
    /// Gets or sets the typographic line gap.
    /// </summary>
    public short STypoLineGap { get; set; }

    /// <summary>
    /// Gets or sets the Windows ascender (clipping boundary, not necessarily typographic ascent).
    /// </summary>
    public ushort UsWinAscent { get; set; }

    /// <summary>
    /// Gets or sets the Windows descender (clipping boundary, not necessarily typographic descent).
    /// </summary>
    public ushort UsWinDescent { get; set; }

    /// <summary>
    /// Gets or sets the first code page range bit field. Version 1 and later only.
    /// </summary>
    public uint? UlCodePageRange1 { get; set; }

    /// <summary>
    /// Gets or sets the second code page range bit field. Version 1 and later only.
    /// </summary>
    public uint? UlCodePageRange2 { get; set; }

    /// <summary>
    /// Gets or sets the x-height. Version 2 and later only.
    /// </summary>
    public short? SxHeight { get; set; }

    /// <summary>
    /// Gets or sets the cap height. Version 2 and later only.
    /// </summary>
    public short? SCapHeight { get; set; }

    /// <summary>
    /// Gets or sets the Unicode code point used as the default glyph. Version 2 and later only.
    /// </summary>
    public ushort? UsDefaultChar { get; set; }

    /// <summary>
    /// Gets or sets the Unicode code point used as the default word-break character. Version 2 and
    /// later only.
    /// </summary>
    public ushort? UsBreakChar { get; set; }

    /// <summary>
    /// Gets or sets the maximum length of a target glyph context for any feature in the font.
    /// Version 2 and later only.
    /// </summary>
    public ushort? UsMaxContext { get; set; }

    /// <summary>
    /// Gets or sets the lowest size, in twentieths of a point, at which this font begins to be used.
    /// Version 5 only.
    /// </summary>
    public ushort? UsLowerOpticalPointSize { get; set; }

    /// <summary>
    /// Gets or sets the highest size, in twentieths of a point, at which this font stops being used.
    /// Version 5 only.
    /// </summary>
    public ushort? UsUpperOpticalPointSize { get; set; }
}
