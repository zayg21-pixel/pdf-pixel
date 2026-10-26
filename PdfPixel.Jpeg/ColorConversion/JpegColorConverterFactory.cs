using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using PdfPixel.Jpeg.Decoding;

namespace PdfPixel.Jpeg.ColorConversion;

internal static class JpegColorConverterFactory
{
    public static IJpegColorConverter Create(JpegHeader header, JpegDecodingParameters parameters, JpegDecoderOptions? options = null)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        options ??= JpegDecoderOptions.Default;

        if (header.ComponentCount == 1)
        {
            return new ColorClampConverter(parameters);
        }

        if (header.ComponentCount == 3)
        {
            bool applyYuv = options.YuvMode switch
            {
                JpegYuvMode.NoYuv => false,
                JpegYuvMode.ForceYuv => true,
                _ => IsYuvNeeded(header)
            };
            return applyYuv ? new YcbCrFloatColorConverter(header, parameters) : new ColorClampConverter(parameters);
        }

        if (header.ComponentCount == 4)
        {
            bool applyYcck = options.YuvMode switch
            {
                JpegYuvMode.NoYuv => false,
                JpegYuvMode.ForceYuv => !header.HasAdobeApp14 || header.AdobeColorTransform != 0,
                _ => header.HasAdobeApp14 && header.AdobeColorTransform == 2
            };

            IJpegColorConverter converter = applyYcck
                ? new YcckFloatColorConverter(header, parameters)
                : new ColorClampConverter(parameters);

            return (options.InvertCmykColors)
                ? new CmykInvertingConverter(parameters, converter)
                : converter;
        }

        return new ColorClampConverter(parameters);
    }

    private static bool IsYuvNeeded(JpegHeader header)
    {
        if (header.HasAdobeApp14)
        {
            return header.AdobeColorTransform != 0;
        }

        return !LikelyRgb(header);
    }

    private static bool LikelyRgb(JpegHeader header)
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

    private sealed class ColorClampConverter : IJpegColorConverter
    {
        private readonly int _vectorStride;
        private readonly int _vectorLimit;

        public ColorClampConverter(JpegDecodingParameters parameters)
        {
            _vectorStride = parameters.BlockVectorStride;
            _vectorLimit = parameters.BlockVectorLimit;
        }

#if !NETSTANDARD2_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void ConvertInPlace(Block8x8F[][] upsampledBandBlocks)
        {
            bool wholeBlock = _vectorLimit == Block8x8F.VectorCount;

            foreach (Block8x8F[] band in upsampledBandBlocks)
            {
                foreach (ref Block8x8F block in band.AsSpan())
                {
                    if (wholeBlock)
                    {
                        block.ClampToByte();
                    }
                    else
                    {
                        block.ClampToByte(_vectorStride, _vectorLimit);
                    }
                }
            }
        }
    }

    private sealed class CmykInvertingConverter : IJpegColorConverter
    {
        private static readonly Vector4 _v255 = new(255f);

        private readonly IJpegColorConverter _inner;
        private readonly int _vectorStride;
        private readonly int _vectorLimit;

        public CmykInvertingConverter(JpegDecodingParameters parameters, IJpegColorConverter inner)
        {
            _inner = inner;
            _vectorStride = parameters.BlockVectorStride;
            _vectorLimit = parameters.BlockVectorLimit;
        }

#if !NETSTANDARD2_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void ConvertInPlace(Block8x8F[][] upsampledBandBlocks)
        {
            for (int c = 0; c < 4; c++)
            {
                Block8x8F[] blocks = upsampledBandBlocks[c];
                for (int b = 0; b < blocks.Length; b++)
                {
                    ref Block8x8F block = ref blocks[b];
                    ref Vector4 vecRef = ref Unsafe.As<Block8x8F, Vector4>(ref block);
                    for (int v = 0; v < _vectorLimit; v += _vectorStride)
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
