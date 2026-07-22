using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// The result of evaluating a single "glyf" glyph: its outline and a repacked glyph with hinting
/// instructions stripped. Mirrors <c>PdfPixel.Fonts.CffV2.CffCharacter</c>'s role for CFF charstrings.
/// </summary>
public class SfntGlyphCharacter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntGlyphCharacter"/> class.
    /// </summary>
    /// <param name="path">The glyph outline, encoded in <see cref="PdfPixel.Fonts.Model.PdfFontPathBuilder"/>'s format.</param>
    /// <param name="glyphData">The repacked glyph: same structure as the source (simple or composite), with hinting instructions stripped.</param>
    public SfntGlyphCharacter(in ReadOnlyMemory<byte> path, in ReadOnlyMemory<byte> glyphData)
    {
        Path = path;
        GlyphData = glyphData;
    }

    /// <summary>
    /// Gets the glyph outline, encoded in <see cref="PdfPixel.Fonts.Model.PdfFontPathBuilder"/>'s format.
    /// </summary>
    public ReadOnlyMemory<byte> Path { get; }

    /// <summary>
    /// Gets the repacked glyph: same structure as the source (simple or composite), with hinting
    /// instructions stripped.
    /// </summary>
    public ReadOnlyMemory<byte> GlyphData { get; }
}
