using PdfReader.Color.Sampling;
using PdfReader.Color.Structures;
using System;
using System.Collections.Generic;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Indexed color space converter.
/// </summary>
internal sealed partial class IndexedConverter : PdfColorSpaceConverter
{
    private readonly PdfColorSpaceConverter _baseConv;
    private readonly int _hiVal;
    private readonly byte[] _lookup; // packed by base components (sequential entries)

    // Cache of palettes per rendering intent
    private readonly Dictionary<PdfRenderingIntent, RgbaPacked[]> _paletteCache = new Dictionary<PdfRenderingIntent, RgbaPacked[]>(4);

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
    public RgbaPacked[] BuildPalette(PdfRenderingIntent renderingIntent)
    {
        var sampler = _baseConv.GetRgbaSampler(renderingIntent);
        if (_paletteCache.TryGetValue(renderingIntent, out var existing))
        {
            return existing;
        }

        int baseComps = _baseConv.Components;
        int paletteSize = _hiVal + 1;
        var palette = new RgbaPacked[paletteSize];

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
            RgbaPacked result = default;
            sampler.Sample(comps, ref result);

            palette[index] = result;
        }

        _paletteCache[renderingIntent] = palette;
        return palette;
    }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        return new IndexedSampler(BuildPalette(intent), _hiVal);
    }
}
