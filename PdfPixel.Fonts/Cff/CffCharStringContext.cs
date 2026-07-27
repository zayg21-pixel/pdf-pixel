using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Mutable state threaded through <see cref="CffCharStringEvaluator"/>'s recursive evaluation of a
/// charstring and its inlined subroutine calls.
/// </summary>
internal sealed class CffCharStringContext
{
    public CffCharStringContext(
        ReadOnlyMemory<byte>[] localSubrs,
        ReadOnlyMemory<byte>[] globalSubrs,
        float nominalWidthX,
        float defaultWidthX,
        ReadOnlyMemory<byte>[] charStrings,
        IReadOnlyDictionary<PdfFontString, ushort> nameToGid,
        in PdfFontMatrix matrix)
    {
        LocalSubrs = localSubrs;
        GlobalSubrs = globalSubrs;
        LocalBias = CalculateBias(localSubrs.Length);
        GlobalBias = CalculateBias(globalSubrs.Length);
        NominalWidthX = nominalWidthX;
        DefaultWidthX = defaultWidthX;
        Width = defaultWidthX;
        CharStrings = charStrings;
        NameToGid = nameToGid;
        PathBuilder = new PdfFontPathBuilder(matrix);
    }

    public ReadOnlyMemory<byte>[] LocalSubrs { get; }

    public ReadOnlyMemory<byte>[] GlobalSubrs { get; }

    /// <summary>
    /// Gets this font's raw CharStrings INDEX entries, indexed by GID -- used to resolve the base
    /// and accent glyphs of a deprecated seac-style <c>endchar</c> accent composition.
    /// </summary>
    public ReadOnlyMemory<byte>[] CharStrings { get; }

    /// <summary>
    /// Gets this font's glyph-name-to-GID map -- used to resolve the base and accent glyphs of a
    /// deprecated seac-style <c>endchar</c> accent composition. Empty for CID-keyed fonts, which
    /// don't use this facility.
    /// </summary>
    public IReadOnlyDictionary<PdfFontString, ushort> NameToGid { get; }

    /// <summary>
    /// Gets or sets a value indicating whether evaluation is currently inside a seac accent
    /// composition's base or accent component charstring. Suppresses that component's own
    /// <c>endchar</c> from ending the overall composite glyph.
    /// </summary>
    public bool InSeacComponent { get; set; }

    public int LocalBias { get; }

    public int GlobalBias { get; }

    public float NominalWidthX { get; }

    public float DefaultWidthX { get; }

    public List<float> Operands { get; } = new(32);

    public CffCharStringWriter Writer { get; } = new();

    public PdfFontPathBuilder PathBuilder { get; }

    public float CurrentX { get; set; }

    public float CurrentY { get; set; }

    public bool SawMoveTo { get; set; }

    public bool GotWidth { get; set; }

    public float Width { get; set; }

    public int HintCount { get; set; }

    public int HintMaskBytes { get; set; }

    public bool Finished { get; set; }

    public List<float> PopAll()
    {
        List<float> result = new(Operands);
        Operands.Clear();
        return result;
    }

    private static int CalculateBias(int count)
    {
        if (count < CffConstants.SubrBiasSmallCountThreshold)
        {
            return CffConstants.SubrBiasSmall;
        }

        if (count < CffConstants.SubrBiasMediumCountThreshold)
        {
            return CffConstants.SubrBiasMedium;
        }

        return CffConstants.SubrBiasLarge;
    }
}
