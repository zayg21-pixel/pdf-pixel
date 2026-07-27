using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Type1;

/// <summary>
/// Mutable state threaded through <see cref="Type1CharStringEvaluator"/>'s recursive evaluation of a
/// charstring and its inlined subroutine and seac component calls.
/// </summary>
internal sealed class Type1CharStringContext
{
    public Type1CharStringContext(IReadOnlyDictionary<int, byte[]> localSubrs, IReadOnlyDictionary<PdfFontString, byte[]> charStrings, in PdfFontMatrix matrix)
    {
        LocalSubrs = localSubrs;
        CharStrings = charStrings;
        PathBuilder = new PdfFontPathBuilder(matrix);
    }

    public IReadOnlyDictionary<int, byte[]> LocalSubrs { get; }

    /// <summary>
    /// Gets this font's raw (undecoded-to-Type2) Type1 charstrings by glyph name -- used to resolve the
    /// base and accent glyphs of a <c>seac</c> accent composition.
    /// </summary>
    public IReadOnlyDictionary<PdfFontString, byte[]> CharStrings { get; }

    public List<float> Operands { get; } = new(32);

    public CffCharStringWriter Writer { get; } = new();

    public PdfFontPathBuilder PathBuilder { get; }

    public float CurrentX { get; set; }

    public float CurrentY { get; set; }

    /// <summary>
    /// Gets or sets the side bearing most recently declared by <c>hsbw</c>/<c>sbw</c>, used by
    /// <c>seac</c>'s accent-offset formula.
    /// </summary>
    public float SideBearingX { get; set; }

    public float Width { get; set; }

    /// <summary>
    /// Gets or sets the origin shift owed to the next real <c>moveto</c>, accumulated by <c>hsbw</c>/
    /// <c>sbw</c> (which reposition the current point without themselves drawing anything). Consumed and
    /// reset the moment a real <c>moveto</c> merges it into its own delta.
    /// </summary>
    public float PendingOriginDeltaX { get; set; }

    public float PendingOriginDeltaY { get; set; }

    /// <summary>
    /// Gets or sets the translation applied by <c>hsbw</c>/<c>sbw</c> while evaluating a <c>seac</c>
    /// accent component, on top of the accent's own side bearing. Zero outside of accent evaluation.
    /// </summary>
    public float SeacOffsetX { get; set; }

    public float SeacOffsetY { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether evaluation is currently inside a seac accent
    /// composition's base or accent component charstring. Suppresses that component's own
    /// <c>endchar</c> from ending the overall composite glyph.
    /// </summary>
    public bool InSeacComponent { get; set; }

    public bool SawMoveTo { get; set; }

    public bool InFlexSequence { get; set; }

    public List<float>? FlexDeltas { get; set; }

    public List<float> OtherSubrResults { get; } = [];

    public bool Finished { get; set; }

    public List<float> PopAll()
    {
        List<float> result = new(Operands);
        Operands.Clear();
        return result;
    }
}
