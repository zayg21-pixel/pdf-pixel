using PdfPixel.Objects;
using System;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The palette of an Indexed color space: the colors its indices select, given in its base color space.
/// </summary>
public sealed class PdfPalette
{
    private const float MaximumLookupValue = byte.MaxValue;

    internal PdfPalette(PdfColorSpaceConverter baseColorSpace, int highestIndex, byte[] lookup)
    {
        BaseColorSpace = baseColorSpace ?? throw new ArgumentNullException(nameof(baseColorSpace));
        HighestIndex = Math.Max(0, highestIndex);
        Lookup = lookup ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Gets the color space the palette colors are given in (base).
    /// </summary>
    public PdfColorSpaceConverter BaseColorSpace { get; }

    /// <summary>
    /// Gets the highest valid index (hival).
    /// </summary>
    public int HighestIndex { get; }

    /// <summary>
    /// Gets the palette bytes, one per base component of each color (lookup).
    /// </summary>
    public byte[] Lookup { get; }

    /// <summary>
    /// Returns the color <paramref name="index"/> selects, in <see cref="BaseColorSpace"/>. An index outside
    /// 0 to <see cref="HighestIndex"/> selects the nearest valid color.
    /// </summary>
    public PdfColor GetColor(int index)
    {
        int clampedIndex = Math.Max(0, Math.Min(HighestIndex, index));
        PdfRange[]? componentRanges = BaseColorSpace.ComponentRanges;
        var components = new float[BaseColorSpace.Components];
        int offset = clampedIndex * components.Length;

        for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            int lookupIndex = offset + componentIndex;
            byte lookupValue = (lookupIndex < Lookup.Length) ? Lookup[lookupIndex] : (byte)0;
            float normalizedValue = lookupValue / MaximumLookupValue;

            if (componentRanges != null)
            {
                components[componentIndex] = componentRanges[componentIndex].Denormalize(normalizedValue);
            }
            else
            {
                components[componentIndex] = normalizedValue;
            }
        }

        return new PdfColor(components);
    }

    /// <summary>
    /// Returns the color <paramref name="index"/> selects, in <see cref="BaseColorSpace"/>, rounding it to the nearest
    /// integer with 0.5 rounded up. An index outside 0 to <see cref="HighestIndex"/> selects the nearest valid color.
    /// </summary>
    public PdfColor GetColor(float index) => GetColor((int)Math.Floor(index + 0.5f));
}
