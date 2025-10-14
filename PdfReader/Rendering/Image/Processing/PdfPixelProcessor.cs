using PdfReader.Models;
using PdfReader.Rendering.Color;
using PdfReader.Rendering.Color.Clut;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    internal sealed class PdfPixelProcessor
    {
        private readonly PdfColorSpaceConverter _converter;
        private readonly PdfRenderingIntent _intent;
        private readonly IRgbaSampler _lut;
        private readonly byte[] _decodeArray;
        private readonly int[] _maskArray;
        private readonly int _components;
        private readonly bool _hasDecode;
        private readonly bool _hasMask;
        private readonly bool _isRequired;
        private readonly bool _hasColor;

        public PdfPixelProcessor(PdfImage image)
        {
            _intent = image.RenderingIntent;
            _converter = image.ColorSpaceConverter;
            _lut = _converter.GetSampler(_intent);
            _components = _converter.Components;
            _decodeArray = ProcessingUtilities.BuildDecodeMinSpanBytes(_components, image.DecodeArray, image.HasImageMask);
            _maskArray = ProcessingUtilities.BuildNormalizedMaskRawPairs(_components, image.MaskArray, image.BitsPerComponent);
            _hasDecode = _decodeArray != null;
            _hasMask = _maskArray != null;
            _hasColor = !_lut.IsDefault;
            _isRequired = _hasDecode || _hasMask || !_lut.IsDefault;
        }

        public bool HasProcessing => _isRequired;

        public bool HasDecode => _hasDecode;

        public bool HasMask => _hasMask;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteGray(ref byte gray)
        {
            if (_hasDecode)
            {
                gray = MapDecodedByte(gray, 0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteRgba(ref Rgba pixel)
        {
            if (_hasDecode)
            {
                pixel.R = MapDecodedByte(pixel.R, 0);
                pixel.G = MapDecodedByte(pixel.G, 1);
                pixel.B = MapDecodedByte(pixel.B, 2);
            }
            if (_hasMask)
            {
                ApplyMask(ref pixel);
            }

            if (_hasColor)
            {
                _lut.Sample(ref pixel, ref pixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteGray(ref Rgba pixel)
        {
            if (_hasDecode)
            {
                pixel.R = MapDecodedByte(pixel.R, 0);
            }

            if (_hasColor)
            {
                _lut.Sample(ref pixel, ref pixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteCmyk(ref Rgba pixel)
        {
            if (_hasDecode)
            {
                pixel.R = MapDecodedByte(pixel.R, 0);
                pixel.G = MapDecodedByte(pixel.G, 1);
                pixel.B = MapDecodedByte(pixel.B, 2);
                pixel.A = MapDecodedByte(pixel.A, 3);
            }
            
            if (_hasColor)
            {
                _lut.Sample(ref pixel, ref pixel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyMask(ref Rgba pixel)
        {
            for (int componentIndex = 0; componentIndex < _components; componentIndex++)
            {
                int maskBase = componentIndex * 2;
                int minRaw = _maskArray[maskBase];
                int maxRaw = _maskArray[maskBase + 1];
                int value = componentIndex switch
                {
                    0 => pixel.R,
                    1 => pixel.G,
                    2 => pixel.B,
                    3 => pixel.A,
                    _ => 0
                };
                if (value < minRaw || value > maxRaw)
                {
                    return;
                }
            }

            pixel.A = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte MapDecodedByte(int expanded, int componentIndex)
        {
            int decodePairIndex = componentIndex * 2;
            int minByte = _decodeArray[decodePairIndex];
            int maxByte = _decodeArray[decodePairIndex + 1];
            if (minByte == maxByte)
            {
                return (byte)minByte;
            }
            int span = maxByte - minByte;
            int mappedValue;
            if (span > 0)
            {
                mappedValue = minByte + (expanded * span + 127) / 255;
            }
            else
            {
                span = -span;
                mappedValue = minByte - (expanded * span + 127) / 255;
            }
            if (mappedValue < 0)
            {
                mappedValue = 0;
            }
            else if (mappedValue > 255)
            {
                mappedValue = 255;
            }
            return (byte)mappedValue;
        }
    }
}
