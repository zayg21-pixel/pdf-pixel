using System;
using PdfReader.Rendering.Image.Jpg.Model;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    /// <summary>
    /// MCU writer for grayscale JPEG images.
    /// Converts single-component grayscale data to RGBA output.
    /// </summary>
    internal sealed class GrayMcuWriter : BaseMcuWriter
    {
        private readonly byte[] _tile0;
        private readonly int _tileWidth0;
        private readonly int _verticalSamplingFactor0;
        private readonly int _horizontalSamplingFactor0;

        public GrayMcuWriter(
            JpgHeader header,
            byte[][] componentTiles,
            int[] tileWidths,
            int hMax,
            int vMax,
            int mcuWidth,
            int imageWidth,
            int outputStride)
            : base(header, componentTiles, tileWidths, hMax, vMax, mcuWidth, imageWidth, outputStride)
        {
            if (header.ComponentCount != 1)
            {
                throw new ArgumentException("GrayMcuWriter requires exactly 1 component.", nameof(header));
            }

            // Cache component metadata to avoid repeated lookups in hot path
            _tile0 = componentTiles[0];
            _tileWidth0 = tileWidths[0];
            _verticalSamplingFactor0 = header.Components[0].VerticalSamplingFactor;
            _horizontalSamplingFactor0 = header.Components[0].HorizontalSamplingFactor;
        }

        public override void WriteToBuffer(byte[] buffer, int xBase, int heightPixels)
        {
            int accY0 = 0;
            int sy0 = 0;
            int yOff0 = 0;

            for (int ly = 0; ly < heightPixels; ly++)
            {
                int destRow = ly * _outputStride;
                int destOffset = destRow + xBase; // 1 byte per pixel

                int accX0 = 0;
                int sx0 = 0;

                for (int lx = 0; lx < _mcuWidth && xBase + lx < _imageWidth; lx++)
                {
                    buffer[destOffset] = _tile0[yOff0 + sx0];
                    destOffset += 1;

                    accX0 += _horizontalSamplingFactor0;
                    if (accX0 >= _hMax)
                    {
                        accX0 -= _hMax;
                        sx0++;
                    }
                }

                accY0 += _verticalSamplingFactor0;
                if (accY0 >= _vMax)
                {
                    accY0 -= _vMax;
                    sy0++;
                    yOff0 = sy0 * _tileWidth0;
                }
            }
        }
    }
}