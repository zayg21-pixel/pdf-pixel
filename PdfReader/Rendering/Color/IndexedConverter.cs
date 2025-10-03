using PdfReader.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Color
{
    internal sealed class IndexedConverter : PdfColorSpaceConverter
    {
        private readonly PdfColorSpaceConverter _baseConv;
        private readonly int _hiVal;
        private readonly byte[] _lookup; // packed by base components (sequential entries)

        // Cache of palettes per rendering intent
        private readonly Dictionary<PdfRenderingIntent, SKColor[]> _paletteCache = new Dictionary<PdfRenderingIntent, SKColor[]>(4);
        private readonly object _paletteLock = new object();

        public IndexedConverter(PdfColorSpaceConverter baseConv, int hiVal, byte[] lookup)
        {
            _baseConv = baseConv ?? throw new ArgumentNullException(nameof(baseConv));
            _hiVal = Math.Max(0, hiVal);
            _lookup = lookup ?? Array.Empty<byte>();
        }

        public override int Components => 1;

        public override bool IsDevice => false;

        private int ClampIndex(float rawIndex)
        {
            if (float.IsNaN(rawIndex))
            {
                return 0;
            }

            int idx = (int)Math.Round(rawIndex);
            if (idx < 0)
            {
                return 0;
            }
            if (idx > _hiVal)
            {
                return _hiVal;
            }
            return idx;
        }

        /// <summary>
        /// Build (or retrieve cached) palette for this Indexed color space under the specified rendering intent.
        /// </summary>
        public SKColor[] BuildPalette(PdfRenderingIntent renderingIntent)
        {
            lock (_paletteLock)
            {
                if (_paletteCache.TryGetValue(renderingIntent, out var existing))
                {
                    return existing;
                }

                int baseComps = _baseConv.Components;
                int paletteSize = _hiVal + 1;
                var palette = new SKColor[paletteSize];
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

                    palette[index] = _baseConv.ToSrgb(comps, renderingIntent);
                }

                _paletteCache[renderingIntent] = palette;
                return palette;
            }
        }

        public override SKColor ToSrgb(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
        {
            int idx = 0;
            if (comps01.Length > 0)
            {
                idx = ClampIndex(comps01[0]);
            }

            // Always retrieve (and lazily build) the palette for simplicity and consistency.
            var palette = BuildPalette(renderingIntent);
            if (idx >= 0 && idx < palette.Length)
            {
                return palette[idx];
            }
            return SKColors.Black;
        }
    }
}
