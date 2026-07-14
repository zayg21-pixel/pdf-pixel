using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Models;

/// <summary>
/// A precomputed [min, max] interval parsed from a PDF array, with cached range and inverse range
/// so repeated normalization/denormalization can use multiplication instead of division.
/// </summary>
public readonly struct PdfRange
{
    /// <summary>
    /// Initializes a new <see cref="PdfRange"/> from explicit bounds.
    /// </summary>
    public PdfRange(float min, float max)
    {
        Min = min;
        Max = max;
        Range = max - min;
        InverseRange = (Range != 0f) ? 1f / Range : 0f;
    }

    /// <summary>
    /// Lower bound of the interval.
    /// </summary>
    public float Min { get; }

    /// <summary>
    /// Upper bound of the interval.
    /// </summary>
    public float Max { get; }

    /// <summary>
    /// Pre-calculated <see cref="Max"/> minus <see cref="Min"/>.
    /// </summary>
    public float Range { get; }

    /// <summary>
    /// Pre-calculated reciprocal of <see cref="Range"/>, or 0 when the interval is degenerate.
    /// </summary>
    public float InverseRange { get; }

    /// <summary>
    /// Maps <paramref name="value"/> from this interval to a fraction (0 at <see cref="Min"/>, 1 at <see cref="Max"/>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Normalize(float value) => (value - Min) * InverseRange;

    /// <summary>
    /// Maps a fraction <paramref name="t"/> (0 at <see cref="Min"/>, 1 at <see cref="Max"/>) back into this interval.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Denormalize(float t) => Min + (t * Range);

    /// <summary>
    /// Clamps <paramref name="value"/> to [<see cref="Min"/>, <see cref="Max"/>].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Clamp(float value) => Math.Max(Min, Math.Min(Max, value));

    /// <summary>
    /// Parses a flat PDF array of interleaved [min0, max0, min1, max1, ...] pairs into one
    /// <see cref="PdfRange"/> per pair.
    /// </summary>
    /// <param name="items">Interleaved min/max pairs, typically from <see cref="PdfArrayExtensions.GetFloatArray"/>.</param>
    /// <returns>One <see cref="PdfRange"/> per pair; empty when <paramref name="items"/> has fewer than 2 entries.</returns>
    public static PdfRange[] FromArray(float[]? items)
    {
        if (items == null || items.Length < 2)
        {
            return Array.Empty<PdfRange>();
        }

        int count = items.Length / 2;
        var ranges = new PdfRange[count];
        for (int index = 0; index < count; index++)
        {
            ranges[index] = new PdfRange(items[2 * index], items[(2 * index) + 1]);
        }

        return ranges;
    }
}
