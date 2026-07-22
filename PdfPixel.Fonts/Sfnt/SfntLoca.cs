using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a parsed SFNT "loca" table: each glyph's byte offset into "glyf", plus one trailing
/// entry giving "glyf"'s total length - so a glyph's byte length is
/// <c>Offsets[gid + 1] - Offsets[gid]</c>, and a length of 0 means the glyph has no outline (e.g. space).
/// </summary>
public class SfntLoca
{
    /// <summary>
    /// Gets or sets each glyph's byte offset into "glyf", with one trailing entry for the table's
    /// total length. Has <c>numGlyphs + 1</c> entries.
    /// </summary>
    public IReadOnlyList<uint> Offsets { get; set; } = [];
}
