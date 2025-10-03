using System;
using PdfReader.Rendering.Image.Jpg.Model;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    /// <summary>
    /// MCU writer for RGB JPEG images.
    /// Writes raw R, G, B bytes (3 bytes per pixel) without alpha.
    /// </summary>
    internal sealed class RgbMcuWriter : BaseMcuWriter
    {
        private readonly byte[] _tile0;
        private readonly byte[] _tile1;
        private readonly byte[] _tile2;
        private readonly int _tileWidth0;
        private readonly int _tileWidth1;
        private readonly int _tileWidth2;
        private readonly int _verticalSamplingFactor0;
        private readonly int _verticalSamplingFactor1;
        private readonly int _verticalSamplingFactor2;
        private readonly int _horizontalSamplingFactor0;
        private readonly int _horizontalSamplingFactor1;
        private readonly int _horizontalSamplingFactor2;

        public RgbMcuWriter(
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
            if (header.ComponentCount != 3)
            {
                throw new ArgumentException("RgbMcuWriter requires exactly 3 components.", nameof(header));
            }
            _tile0 = componentTiles[0];
            _tile1 = componentTiles[1];
            _tile2 = componentTiles[2];
            _tileWidth0 = tileWidths[0];
            _tileWidth1 = tileWidths[1];
            _tileWidth2 = tileWidths[2];
            _verticalSamplingFactor0 = header.Components[0].VerticalSamplingFactor;
            _verticalSamplingFactor1 = header.Components[1].VerticalSamplingFactor;
            _verticalSamplingFactor2 = header.Components[2].VerticalSamplingFactor;
            _horizontalSamplingFactor0 = header.Components[0].HorizontalSamplingFactor;
            _horizontalSamplingFactor1 = header.Components[1].HorizontalSamplingFactor;
            _horizontalSamplingFactor2 = header.Components[2].HorizontalSamplingFactor;
        }

        public override void WriteToBuffer(byte[] buffer, int xBase, int heightPixels)
        {
            int accY0 = 0; int sy0 = 0; int yOff0 = 0;
            int accY1 = 0; int sy1 = 0; int yOff1 = 0;
            int accY2 = 0; int sy2 = 0; int yOff2 = 0;
            for (int ly = 0; ly < heightPixels; ly++)
            {
                int destRow = ly * _outputStride;
                int destOffset = destRow + xBase * 3;
                int accX0 = 0; int sx0 = 0;
                int accX1 = 0; int sx1 = 0;
                int accX2 = 0; int sx2 = 0;
                for (int lx = 0; lx < _mcuWidth && xBase + lx < _imageWidth; lx++)
                {
                    buffer[destOffset + 0] = _tile0[yOff0 + sx0];
                    buffer[destOffset + 1] = _tile1[yOff1 + sx1];
                    buffer[destOffset + 2] = _tile2[yOff2 + sx2];
                    destOffset += 3;
                    accX0 += _horizontalSamplingFactor0; if (accX0 >= _hMax) { accX0 -= _hMax; sx0++; }
                    accX1 += _horizontalSamplingFactor1; if (accX1 >= _hMax) { accX1 -= _hMax; sx1++; }
                    accX2 += _horizontalSamplingFactor2; if (accX2 >= _hMax) { accX2 -= _hMax; sx2++; }
                }
                accY0 += _verticalSamplingFactor0; if (accY0 >= _vMax) { accY0 -= _vMax; sy0++; yOff0 = sy0 * _tileWidth0; }
                accY1 += _verticalSamplingFactor1; if (accY1 >= _vMax) { accY1 -= _vMax; sy1++; yOff1 = sy1 * _tileWidth1; }
                accY2 += _verticalSamplingFactor2; if (accY2 >= _vMax) { accY2 -= _vMax; sy2++; yOff2 = sy2 * _tileWidth2; }
            }
        }
    }
}