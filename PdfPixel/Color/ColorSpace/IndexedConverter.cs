using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.ColorSpace;

internal sealed class IndexedConverter : PdfColorSpaceConverter
{
    private readonly PdfColorSpaceConverter _baseConv;
    private readonly int _hiVal;
    private readonly byte[] _lookup;

    private readonly Dictionary<PaletteCacheKey, Vector4[]> _paletteCache = [];

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
    public Vector4[] BuildPalette(PdfRenderingIntent renderingIntent, TransferFunctionTransform? postTransform)
    {
        PaletteCacheKey key = new(renderingIntent, postTransform);
        if (_paletteCache.TryGetValue(key, out Vector4[]? existing))
        {
            return existing;
        }

        ColorTransformSampler sampler = _baseConv.GetRgbaSampler(renderingIntent, postTransform, normalize: false);

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

        _paletteCache[key] = palette;
        return palette;
    }

    public RgbaPacked[] BuildPackedPalette(PdfRenderingIntent renderingIntent, TransferFunctionTransform? postTransform)
    {
        Vector4[] palette = BuildPalette(renderingIntent, postTransform);
        int paletteSize = palette.Length;
        var packedPalette = new RgbaPacked[paletteSize];
        for (int i = 0; i < paletteSize; i++)
        {

            packedPalette[i] = palette[i].From01ToRgba();
        }

        return packedPalette;
    }

    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
        => new(new ChainedColorTransform(new IndexedColorTransform(BuildPalette(intent, postTransform), _hiVal)));

    private readonly struct PaletteCacheKey : IEquatable<PaletteCacheKey>
    {
        private readonly PdfRenderingIntent _intent;
        private readonly TransferFunctionTransform? _postTransform;

        public PaletteCacheKey(PdfRenderingIntent intent, TransferFunctionTransform? postTransform)
        {
            _intent = intent;
            _postTransform = postTransform;
        }

        public bool Equals(PaletteCacheKey other)
            => _intent == other._intent && ReferenceEquals(_postTransform, other._postTransform);

        public override bool Equals(object? obj) => obj is PaletteCacheKey k && Equals(k);

        public override int GetHashCode() => HashCode.Combine(_intent, RuntimeHelpers.GetHashCode(_postTransform));
    }
}
