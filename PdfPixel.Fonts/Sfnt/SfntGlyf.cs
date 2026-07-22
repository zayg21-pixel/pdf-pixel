using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents an evaluated SFNT "glyf" table: every glyph's outline and repacked data, indexed by
/// glyph ID. A null entry means that glyph has no outline (e.g. space).
/// </summary>
public class SfntGlyf
{
    /// <summary>
    /// Gets or sets every glyph, indexed by glyph ID.
    /// </summary>
    public IReadOnlyList<SfntGlyphCharacter?> Glyphs { get; set; } = [];
}
