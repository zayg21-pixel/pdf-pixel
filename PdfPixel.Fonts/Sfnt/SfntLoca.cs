using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "loca" table: each glyph's byte range into "glyf", one entry per glyph.
/// </summary>
public class SfntLoca
{
    /// <summary>
    /// Gets or sets each glyph's byte range into "glyf", indexed by glyph id.
    /// </summary>
    public IReadOnlyList<SfntGlyphRange> Ranges { get; set; } = [];
}
