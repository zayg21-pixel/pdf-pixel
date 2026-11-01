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
    private readonly Dictionary<PdfColorConversionParameters, Vector4[]> _paletteCache = [];

    internal PdfIndexedColorSpaceConverter(PdfColorSpaceConverter baseColorSpace, int highestIndex, byte[] lookup)
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

    /// <summary>
    /// Build (or retrieve cached) palette for this Indexed color space under the specified color conversion parameters.
    /// </summary>
    public Vector4[] BuildPalette(in PdfColorConversionParameters parameters)
    {
        if (_paletteCache.TryGetValue(parameters, out Vector4[]? existing))
        {
            return existing;
        }

        ColorTransformSampler sampler = BaseColorSpace.GetRgbaSampler(parameters, normalize: false);

        int baseComps = BaseColorSpace.Components;
        int paletteSize = HighestIndex + 1;
        var palette = new Vector4[paletteSize];

        var comps = new float[baseComps];

        for (int index = 0; index < paletteSize; index++)
        {
            int offset = index * baseComps;
            for (int c = 0; c < baseComps; c++)
            {
                int p = offset + c;
                byte b = (p >= 0 && p < Lookup.Length) ? Lookup[p] : (byte)0;
                comps[c] = b / 255f;
            }

            palette[index] = sampler.Sample(comps);
        }

        _paletteCache[parameters] = palette;
        return palette;
    }

    /// <summary>
    /// Build (or retrieve cached) palette for this Indexed color space under the specified color
    /// conversion parameters, packed as RGBA bytes.
    /// </summary>
    public RgbaPacked[] BuildPackedPalette(in PdfColorConversionParameters parameters)
    {
        Vector4[] palette = BuildPalette(parameters);
        int paletteSize = palette.Length;
        var packedPalette = new RgbaPacked[paletteSize];
        for (int i = 0; i < paletteSize; i++)
        {
            ColorVectorUtilities.Load01ToRgba(palette[i], ref packedPalette[i]);
        }

        return packedPalette;
    }

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(in PdfColorConversionParameters parameters, bool normalize)
        => new(new ChainedColorTransform(new IndexedColorTransform(BuildPalette(parameters), HighestIndex)));

    /// <inheritdoc />
    public override ChainedColorTransform? GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation) => null;
}
