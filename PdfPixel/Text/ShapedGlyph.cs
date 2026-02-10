using PdfPixel.Fonts.Model;

namespace PdfPixel.Text;

/// <summary>
/// Represents a shaped glyph with its width and optional additional advance after the glyph.
/// </summary>
public readonly struct ShapedGlyph
{
    public ShapedGlyph(
        PdfCharacterInfo pdfCharacterInfo,
        int? groupId,
        uint glyphId,
        float advance,
        float scale,
        float x,
        float y)
    {
        CharacterInfo = pdfCharacterInfo;
        GroupId = groupId;
        GlyphId = glyphId;
        Advance = advance;
        Scale = scale;
        X = x;
        Y = y;
    }

    /// <summary>
    /// Original character information.
    /// </summary>
    public PdfCharacterInfo CharacterInfo { get; }

    /// <summary>
    /// Gets the glyph identifier.
    /// </summary>
    public uint GlyphId { get; }

    /// <summary>
    /// If appears in the of multiple glyphs for a single character, indicates the group index.
    /// Then <see cref="CharacterInfo"/> can be used to retrieve the original character information.
    /// </summary>
    public int? GroupId { get; }

    /// <summary>
    /// Advance after X/Y till the edge depending of writing mode.
    /// </summary>
    public float Advance { get; }

    /// <summary>
    /// Scale factor applied to the glyph.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// X position of the glyph.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Y position of the glyph.
    /// </summary>
    public float Y { get; } 
}