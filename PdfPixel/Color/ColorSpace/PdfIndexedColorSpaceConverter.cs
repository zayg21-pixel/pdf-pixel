using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
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
    private readonly PdfColorSpaceConverter _baseConv;
    private readonly int _hiVal;
    private readonly byte[] _lookup;

    private readonly Dictionary<PdfColorConversionParameters, Vector4[]> _paletteCache = [];

    /// <summary>
    /// Initializes the space from its base color space, its highest valid index, and the bytes of
    /// its palette.
    /// </summary>
    public PdfIndexedColorSpaceConverter(PdfColorSpaceConverter baseConv, int hiVal, byte[] lookup)
    {
        _baseConv = baseConv ?? throw new ArgumentNullException(nameof(baseConv));
        _hiVal = Math.Max(0, hiVal);
        _lookup = lookup ?? Array.Empty<byte>();
    }

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

        ColorTransformSampler sampler = _baseConv.GetRgbaSampler(parameters, normalize: false);

        int baseComps = _baseConv.Components;
        int paletteSize = _hiVal + 1;
        var palette = new Vector4[paletteSize];

        var comps = new float[baseComps];

        for (int index = 0; index < paletteSize; index++)
        {
            int offset = index * baseComps;
            for (int c = 0; c < baseComps; c++)
            {
                int p = offset + c;
                byte b = (p >= 0 && p < _lookup.Length) ? _lookup[p] : (byte)0;
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
        => new(new ChainedColorTransform(new IndexedColorTransform(BuildPalette(parameters), _hiVal)));
}
