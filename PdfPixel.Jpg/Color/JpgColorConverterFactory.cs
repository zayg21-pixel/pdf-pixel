using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using PdfPixel.Jpg.Decoding;
using PdfPixel.Jpg.Model;

namespace PdfPixel.Jpg.Color;

internal static class JpgColorConverterFactory
{
    public static IJpgColorConverter Create(JpgHeader header, JpgDecodingParameters parameters, JpgDecoderOptions? options = null)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        options ??= JpgDecoderOptions.Default;

        if (header.ComponentCount == 1)
        {
            return new ColorClampConverter();
        }

        if (header.ComponentCount == 3)
        {
            bool applyYuv = options.YuvMode switch
            {
                JpgYuvMode.NoYuv => false,
                JpgYuvMode.ForceYuv => true,
                _ => IsYuvNeeded(header)
            };
            return applyYuv ? new YcbCrFloatColorConverter(header, parameters) : new ColorClampConverter();
        }

        if (header.ComponentCount == 4)
        {
            bool applyYcck = options.YuvMode switch
            {
                JpgYuvMode.NoYuv => false,
                JpgYuvMode.ForceYuv => !header.HasAdobeApp14 || header.AdobeColorTransform != 0,
                _ => header.HasAdobeApp14 && header.AdobeColorTransform == 2
            };

            IJpgColorConverter converter = applyYcck
                ? new YcckFloatColorConverter(header, parameters)
                : new ColorClampConverter();

            return (options.InvertCmykColors)
                ? new CmykInvertingConverter(converter)
                : converter;
        }

        return new ColorClampConverter();
    }

    private static bool IsYuvNeeded(JpgHeader header)
    {
        if (header.HasAdobeApp14)
        {
            return header.AdobeColorTransform != 0;
        }

        return !LikelyRgb(header);
    }

    private static bool LikelyRgb(JpgHeader header)
    {
        if (header.Components == null || header.Components.Count != 3)
        {
            return false;
        }

        byte r = header.Components[0].Id;
        byte g = header.Components[1].Id;
        byte b = header.Components[2].Id;

        return (r == (byte)'R' || r == (byte)'r')
            && (g == (byte)'G' || g == (byte)'g')
            && (b == (byte)'B' || b == (byte)'b');
    }

    private sealed class ColorClampConverter : IJpgColorConverter
    {
        public void ConvertInPlace(Block8x8F[][] upsampledBandBlocks)
        {
            foreach (Block8x8F[] band in upsampledBandBlocks)
            {
                foreach (ref Block8x8F block in band.AsSpan())
                {
                    block.ClampToByte();
                }
            }
        }
    }

    private sealed class CmykInvertingConverter : IJpgColorConverter
    {
        private static readonly Vector4 _v255 = new(255f);

        private readonly IJpgColorConverter _inner;

        public CmykInvertingConverter(IJpgColorConverter inner) => _inner = inner;

        public void ConvertInPlace(Block8x8F[][] upsampledBandBlocks)
        {
            for (int c = 0; c < 4; c++)
            {
                Block8x8F[] blocks = upsampledBandBlocks[c];
                for (int b = 0; b < blocks.Length; b++)
                {
                    ref Block8x8F block = ref blocks[b];
                    ref Vector4 vecRef = ref Unsafe.As<Block8x8F, Vector4>(ref block);
                    for (int v = 0; v < Block8x8F.VectorCount; v++)
                    {
                        ref Vector4 lane = ref Unsafe.Add(ref vecRef, v);
                        lane = _v255 - lane;
                    }
                }
            }

            _inner.ConvertInPlace(upsampledBandBlocks);
        }
    }
}
