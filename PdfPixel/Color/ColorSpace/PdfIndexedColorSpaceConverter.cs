using PdfPixel.Color.Transform;
using PdfPixel.Objects;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The Indexed color space: color values are indices into a palette of colors defined in a base
/// color space.
/// </summary>
public sealed class PdfIndexedColorSpaceConverter : PdfColorSpaceConverter
{
    /// <summary>
    /// Initializes an Indexed color space.
    /// </summary>
    /// <param name="baseColorSpace">The color space the palette entries are given in.</param>
    /// <param name="highestIndex">The highest valid index.</param>
    /// <param name="lookup">The palette, one byte per base component for each index from 0 to <paramref name="highestIndex"/>.</param>
    public PdfIndexedColorSpaceConverter(PdfColorSpaceConverter baseColorSpace, int highestIndex, byte[] lookup)
    {
        BaseColorSpace = baseColorSpace ?? throw new ArgumentNullException(nameof(baseColorSpace));
        HighestIndex = Math.Max(0, highestIndex);
        Lookup = lookup ?? Array.Empty<byte>();
        Palette = new PdfPalette(BaseColorSpace, HighestIndex, Lookup);
        ComponentRanges = new PdfRange[] { new(0f, HighestIndex) };
    }

    /// <inheritdoc />
    public override PdfRange[]? ComponentRanges { get; }

    /// <summary>
    /// Gets the color space the palette entries are given in (base).
    /// </summary>
    public PdfColorSpaceConverter BaseColorSpace { get; }

    /// <summary>
    /// Gets the palette the indices select colors from.
    /// </summary>
    public PdfPalette Palette { get; }

    /// <summary>
    /// Gets the highest valid index (hival).
    /// </summary>
    public int HighestIndex { get; }

    /// <summary>
    /// Gets the palette, one byte per base component for each index from 0 to <see cref="HighestIndex"/> (lookup).
    /// </summary>
    public byte[] Lookup { get; }

    /// <inheritdoc />
    public override int Components => 1;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
    public override ChainedColorTransform? GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation) => null;

    /// <inheritdoc />
    public override ChainedColorTransform? GetFromProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation) => null;
}
