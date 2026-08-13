using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// The result of evaluating a single CFF charstring: a repacked hint-free/subroutine-free charstring
/// and its width, with the outline built on demand.
/// </summary>
public class CffCharacter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CffCharacter"/> class.
    /// </summary>
    /// <param name="charString">The repacked Type2 charstring: subroutine calls inlined, hints dropped.</param>
    /// <param name="width">The glyph's width in font design units.</param>
    /// <param name="dict">For a CID-keyed font, the FDArray entry this specific character's FDSelect
    /// entry names. Null for a non-CID font, which has no per-character Font DICT.</param>
    public CffCharacter(in ReadOnlyMemory<byte> charString, float width, CffFontDict? dict)
    {
        CharString = charString;
        Width = width;
        Dict = dict;
    }

    /// <summary>
    /// Gets or sets the glyph outline, encoded in <see cref="PdfPixel.Fonts.Model.PdfFontPathBuilder"/>'s
    /// format. Empty until built on demand from <see cref="CharString"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Path { get; set; }

    /// <summary>
    /// Gets the repacked Type2 charstring: subroutine calls inlined, hints dropped, with a leading
    /// width operand present when it differs from the font's default.
    /// </summary>
    public ReadOnlyMemory<byte> CharString { get; }

    /// <summary>
    /// Gets the glyph's width in font design units.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Gets, for a CID-keyed font, the FDArray entry this specific character's FDSelect entry names.
    /// Null for a non-CID font, which has no per-character Font DICT.
    /// </summary>
    public CffFontDict? Dict { get; }

    /// <summary>
    /// Combines <paramref name="font"/>'s Top DICT FontMatrix with this character's own <see cref="Dict"/>
    /// (if any), applying <see cref="Dict"/>'s matrix first, then the font's. A missing Top DICT
    /// FontMatrix combines as identity when <see cref="Dict"/> has its own; otherwise as the ordinary
    /// CFF default.
    /// </summary>
    public PdfFontMatrix GetEffectiveMatrix(CffFont font)
    {
        if (font == null)
        {
            throw new ArgumentNullException(nameof(font));
        }

        return CffTopDict.CombineFontMatrix(font.Dict.TopDict, Dict?.TopDict);
    }
}
