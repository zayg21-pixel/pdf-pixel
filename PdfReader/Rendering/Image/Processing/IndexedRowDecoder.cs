using SkiaSharp;
using PdfReader.Rendering.Color;
using System.Runtime.CompilerServices;
using PdfReader.Models;

namespace PdfReader.Rendering.Image.Processing
{
    internal class IndexedRowDecoder
    {
        private readonly SKColor[] _indexedPalette;
        private readonly int _width;
        private readonly int _bitsPerComponent;
        private readonly int _components;

        public IndexedRowDecoder(IndexedConverter indexedConverter, PdfRenderingIntent intent, int width, int bitsPerComponent)
        {
            _indexedPalette = indexedConverter.BuildPalette(intent);
            _width = width;
            _bitsPerComponent = bitsPerComponent;
            _components = indexedConverter.Components;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte sourceRow, ref byte destRow)
        {
            int hiVal = _indexedPalette.Length - 1;
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                int rgbaBase = columnIndex * 4;
                int sampleIndex = columnIndex * _components;
                int rawIndex = PdfImageRgbaUpsampler.ReadRawSample(ref sourceRow, sampleIndex, _bitsPerComponent);
                if (rawIndex > hiVal)
                {
                    rawIndex = hiVal;
                }
                SKColor color = _indexedPalette[rawIndex];
                Unsafe.Add(ref destRow, rgbaBase) = color.Red;
                Unsafe.Add(ref destRow, rgbaBase + 1) = color.Green;
                Unsafe.Add(ref destRow, rgbaBase + 2) = color.Blue;
                Unsafe.Add(ref destRow, rgbaBase + 3) = 255;
            }
        }
    }
}
