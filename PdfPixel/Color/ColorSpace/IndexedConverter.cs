using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Indexed color space converter.
/// </summary>
internal sealed partial class IndexedConverter : PdfColorSpaceConverter
{
    private readonly PdfColorSpaceConverter _baseConv;
    private readonly int _hiVal;
    private readonly byte[] _lookup; // packed by base components (sequential entries)

    // Cache of palettes per rendering intent
    private readonly Dictionary<PdfRenderingIntent, Vector4[]> _paletteCache = new Dictionary<PdfRenderingIntent, Vector4[]>(4); // TODO: improve caching strategy!

    public IndexedConverter(PdfColorSpaceConverter baseConv, int hiVal, byte[] lookup)
    {
        _baseConv = baseConv ?? throw new ArgumentNullException(nameof(baseConv));
        _hiVal = Math.Max(0, hiVal);
        _lookup = lookup ?? Array.Empty<byte>();
    }

    public override int Components => 1;

    public override bool IsDevice => false;

    /// <summary>
    /// Build (or retrieve cached) palette for this Indexed color space under the specified rendering intent.
    /// </summary>
    public Vector4[] BuildPalette(PdfRenderingIntent renderingIntent, IColorTransform postTransform)
    {
        var sampler = _baseConv.GetRgbaSampler(renderingIntent, postTransform);
        if (_paletteCache.TryGetValue(renderingIntent, out var existing))
        {
            return existing;
        }

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
                byte b = p >= 0 && p < _lookup.Length ? _lookup[p] : (byte)0;
                comps[c] = b / 255f;
            }

            palette[index] = sampler.Sample(comps);
        }

        _paletteCache[renderingIntent] = palette;
        return palette;
    }

    public RgbaPacked[] BuildPackedPalette(PdfRenderingIntent renderingIntent, IColorTransform postTransform)
    {
        var palette = BuildPalette(renderingIntent, postTransform);
        int paletteSize = palette.Length;
        var packedPalette = new RgbaPacked[paletteSize];
        for (int i = 0; i < paletteSize; i++)
        {
            
            packedPalette[i] = ColorVectorUtilities.From01ToRgba(palette[i]);
        }
        return packedPalette;
    }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform postTransform)
    {
        return new IndexedSampler(BuildPalette(intent, postTransform), _hiVal);
    }
}
