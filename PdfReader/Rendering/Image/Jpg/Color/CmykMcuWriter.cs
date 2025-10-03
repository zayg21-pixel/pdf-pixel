using System;
using PdfReader.Rendering.Image.Jpg.Model;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    /// <summary>
    /// MCU writer for CMYK JPEG images.
    /// Writes raw CMYK component values without converting to RGB.
    /// The four component bytes (C, M, Y, K) are stored directly into the destination
    /// buffer's four channels (R, G, B, A) in that order so that downstream post-processing
    /// can perform proper CMYK / ICC based conversion. Alpha is repurposed to carry K.
    /// </summary>
    internal sealed class CmykMcuWriter : BaseMcuWriter
    {
        private readonly byte[] _tile0;
        private readonly byte[] _tile1;
        private readonly byte[] _tile2;
        private readonly byte[] _tile3;
        private readonly int _tileWidth0;
        private readonly int _tileWidth1;
        private readonly int _tileWidth2;
        private readonly int _tileWidth3;
        private readonly int _verticalSamplingFactor0;
        private readonly int _verticalSamplingFactor1;
        private readonly int _verticalSamplingFactor2;
        private readonly int _verticalSamplingFactor3;
        private readonly int _horizontalSamplingFactor0;
        private readonly int _horizontalSamplingFactor1;
        private readonly int _horizontalSamplingFactor2;
        private readonly int _horizontalSamplingFactor3;

        public CmykMcuWriter(
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
            if (header.ComponentCount != 4)
            {
                throw new ArgumentException("CmykMcuWriter requires exactly 4 components.", nameof(header));
            }
            _tile0 = componentTiles[0];
            _tile1 = componentTiles[1];
            _tile2 = componentTiles[2];
            _tile3 = componentTiles[3];
            _tileWidth0 = tileWidths[0];
            _tileWidth1 = tileWidths[1];
            _tileWidth2 = tileWidths[2];
            _tileWidth3 = tileWidths[3];
            _verticalSamplingFactor0 = header.Components[0].VerticalSamplingFactor;
            _verticalSamplingFactor1 = header.Components[1].VerticalSamplingFactor;
            _verticalSamplingFactor2 = header.Components[2].VerticalSamplingFactor;
            _verticalSamplingFactor3 = header.Components[3].VerticalSamplingFactor;
            _horizontalSamplingFactor0 = header.Components[0].HorizontalSamplingFactor;
            _horizontalSamplingFactor1 = header.Components[1].HorizontalSamplingFactor;
            _horizontalSamplingFactor2 = header.Components[2].HorizontalSamplingFactor;
            _horizontalSamplingFactor3 = header.Components[3].HorizontalSamplingFactor;
        }

        public override void WriteToBuffer(byte[] buffer, int xBase, int heightPixels)
        {
            int accY0 = 0; int sy0 = 0; int yOff0 = 0;
            int accY1 = 0; int sy1 = 0; int yOff1 = 0;
            int accY2 = 0; int sy2 = 0; int yOff2 = 0;
            int accY3 = 0; int sy3 = 0; int yOff3 = 0;
            for (int ly = 0; ly < heightPixels; ly++)
            {
                int destRow = ly * _outputStride;
                int destOffset = destRow + xBase * 4; // 4 bytes per pixel (C,M,Y,K)
                int accX0 = 0; int sx0 = 0;
                int accX1 = 0; int sx1 = 0;
                int accX2 = 0; int sx2 = 0;
                int accX3 = 0; int sx3 = 0;
                for (int lx = 0; lx < _mcuWidth && xBase + lx < _imageWidth; lx++)
                {
                    buffer[destOffset + 0] = _tile0[yOff0 + sx0];
                    buffer[destOffset + 1] = _tile1[yOff1 + sx1];
                    buffer[destOffset + 2] = _tile2[yOff2 + sx2];
                    buffer[destOffset + 3] = _tile3[yOff3 + sx3];
                    destOffset += 4;
                    accX0 += _horizontalSamplingFactor0; if (accX0 >= _hMax) { accX0 -= _hMax; sx0++; }
                    accX1 += _horizontalSamplingFactor1; if (accX1 >= _hMax) { accX1 -= _hMax; sx1++; }
                    accX2 += _horizontalSamplingFactor2; if (accX2 >= _hMax) { accX2 -= _hMax; sx2++; }
                    accX3 += _horizontalSamplingFactor3; if (accX3 >= _hMax) { accX3 -= _hMax; sx3++; }
                }
                accY0 += _verticalSamplingFactor0; if (accY0 >= _vMax) { accY0 -= _vMax; sy0++; yOff0 = sy0 * _tileWidth0; }
                accY1 += _verticalSamplingFactor1; if (accY1 >= _vMax) { accY1 -= _vMax; sy1++; yOff1 = sy1 * _tileWidth1; }
                accY2 += _verticalSamplingFactor2; if (accY2 >= _vMax) { accY2 -= _vMax; sy2++; yOff2 = sy2 * _tileWidth2; }
                accY3 += _verticalSamplingFactor3; if (accY3 >= _vMax) { accY3 -= _vMax; sy3++; yOff3 = sy3 * _tileWidth3; }
            }
        }
    }
}