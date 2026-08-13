using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Mutable state threaded through <see cref="CffCharStringEvaluator"/>'s recursive evaluation of a
/// charstring and its inlined subroutine calls. Supplied by the caller and reusable across glyphs.
/// A null <see cref="PathBuilder"/> evaluates without building an outline, a null
/// <see cref="Writer"/> without emitting a repacked charstring.
/// </summary>
internal sealed class CffCharStringContext
{
    private ReadOnlyMemory<byte>[] _localSubrs = Array.Empty<ReadOnlyMemory<byte>>();
    private ReadOnlyMemory<byte>[] _globalSubrs = Array.Empty<ReadOnlyMemory<byte>>();

    /// <summary>
    /// Gets or sets the outline being built, or null to evaluate without building one.
    /// </summary>
    public PdfFontPathBuilder? PathBuilder { get; set; }

    /// <summary>
    /// Gets or sets the repacked charstring being written, or null to evaluate without emitting one.
    /// </summary>
    public CffCharStringWriter? Writer { get; set; }

    /// <summary>
    /// Gets or sets the Local Subr INDEX entries in scope. Setting this recomputes <see cref="LocalBias"/>.
    /// </summary>
    public ReadOnlyMemory<byte>[] LocalSubrs
    {
        get => _localSubrs;

        set
        {
            _localSubrs = value;
            LocalBias = CalculateBias(value.Length);
        }
    }

    /// <summary>
    /// Gets or sets the Global Subr INDEX entries. Setting this recomputes <see cref="GlobalBias"/>.
    /// </summary>
    public ReadOnlyMemory<byte>[] GlobalSubrs
    {
        get => _globalSubrs;

        set
        {
            _globalSubrs = value;
            GlobalBias = CalculateBias(value.Length);
        }
    }

    /// <summary>
    /// Gets this font's raw CharStrings INDEX entries, indexed by GID -- used to resolve the base
    /// and accent glyphs of a deprecated seac-style <c>endchar</c> accent composition.
    /// </summary>
    public ReadOnlyMemory<byte>[] CharStrings { get; set; } = Array.Empty<ReadOnlyMemory<byte>>();

    /// <summary>
    /// Gets or sets this font's glyph-name-to-GID map -- used to resolve the base and accent glyphs of
    /// a deprecated seac-style <c>endchar</c> accent composition. Empty for CID-keyed fonts, which
    /// don't use this facility.
    /// </summary>
    public IReadOnlyDictionary<PdfFontString, ushort> NameToGid { get; set; } = new Dictionary<PdfFontString, ushort>();

    public float NominalWidthX { get; set; }

    public float DefaultWidthX { get; set; }

    public int LocalBias { get; private set; }

    public int GlobalBias { get; private set; }

    public List<float> Operands { get; } = new(32);

    /// <summary>
    /// Gets or sets a value indicating whether evaluation is currently inside a seac accent
    /// composition's base or accent component charstring. Suppresses that component's own
    /// <c>endchar</c> from ending the overall composite glyph.
    /// </summary>
    public bool InSeacComponent { get; set; }

    public float CurrentX { get; set; }

    public float CurrentY { get; set; }

    public bool SawMoveTo { get; set; }

    public bool GotWidth { get; set; }

    public float Width { get; set; }

    public int HintCount { get; set; }

    public int HintMaskBytes { get; set; }

    public bool Finished { get; set; }

    /// <summary>
    /// Clears the state belonging to a single glyph, leaving the font-scope properties in place.
    /// </summary>
    public void Reset()
    {
        Operands.Clear();
        PathBuilder?.Reset();
        Writer?.Reset();
        InSeacComponent = false;
        CurrentX = 0f;
        CurrentY = 0f;
        SawMoveTo = false;
        GotWidth = false;
        Width = DefaultWidthX;
        HintCount = 0;
        HintMaskBytes = 0;
        Finished = false;
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
