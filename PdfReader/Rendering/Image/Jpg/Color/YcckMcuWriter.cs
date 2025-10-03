using System;
using PdfReader.Rendering.Image.Jpg.Model;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    /// <summary>
    /// MCU writer for YCCK JPEG images.
    /// Converts YCCK (Y, Cb, Cr, K) component tiles to CMYK bytes (C, M, Y, K) per pixel using
    /// precomputed YCbCr contribution lookup tables to avoid per-pixel floating point operations.
    /// Steps:
    ///  1. YCbCr -> RGB via: R = Y + rFromCr[Cr]; B = Y + bFromCb[Cb]; G = Y + gFromCb[Cb] + gFromCr[Cr].
    ///  2. RGB -> CMY (invert each channel): C = 255 - R; M = 255 - G; Y = 255 - B.
    ///  3. Preserve original K channel.
    /// Output layout: C, M, Y, K (4 bytes per pixel).
    /// </summary>
    internal sealed class YcckMcuWriter : BaseMcuWriter
    {
        private static readonly short[] _rFromCr = new short[256];
        private static readonly short[] _bFromCb = new short[256];
        private static readonly short[] _gFromCb = new short[256];
        private static readonly short[] _gFromCr = new short[256];

        private readonly byte[] _tile0; // Y
        private readonly byte[] _tile1; // Cb
        private readonly byte[] _tile2; // Cr
        private readonly byte[] _tile3; // K

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

        static YcckMcuWriter()
        {
            // Populate chroma contribution lookup tables once.
            for (int i = 0; i < 256; i++)
            {
                int delta = i - 128;
                _rFromCr[i] = (short)System.Math.Round(1.402000 * delta);
                _bFromCb[i] = (short)System.Math.Round(1.772000 * delta);
                _gFromCb[i] = (short)System.Math.Round(-0.344136 * delta);
                _gFromCr[i] = (short)System.Math.Round(-0.714136 * delta);
            }
        }

        public YcckMcuWriter(
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
                throw new System.ArgumentException("YcckMcuWriter requires exactly 4 components.", nameof(header));
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
            int accY0 = 0;
            int accY1 = 0;
            int accY2 = 0;
            int accY3 = 0;
            int sampleRow0 = 0;
            int sampleRow1 = 0;
            int sampleRow2 = 0;
            int sampleRow3 = 0;
            int yOffset0 = 0;
            int yOffset1 = 0;
            int yOffset2 = 0;
            int yOffset3 = 0;

            for (int localRow = 0; localRow < heightPixels; localRow++)
            {
                int rowOffset = localRow * _outputStride;
                int destOffset = rowOffset + xBase * 4; // C,M,Y,K per pixel

                int accX0 = 0;
                int accX1 = 0;
                int accX2 = 0;
                int accX3 = 0;
                int sx0 = 0;
                int sx1 = 0;
                int sx2 = 0;
                int sx3 = 0;

                for (int localX = 0; localX < _mcuWidth && xBase + localX < _imageWidth; localX++)
                {
                    byte y = _tile0[yOffset0 + sx0];
                    byte cb = _tile1[yOffset1 + sx1];
                    byte cr = _tile2[yOffset2 + sx2];
                    byte k = _tile3[yOffset3 + sx3];

                    int r = y + _rFromCr[cr];
                    int g = y + _gFromCb[cb] + _gFromCr[cr];
                    int b = y + _bFromCb[cb];

                    byte rByte = ClampToByte(r);
                    byte gByte = ClampToByte(g);
                    byte bByte = ClampToByte(b);

                    buffer[destOffset + 0] = (byte)(255 - rByte); // C
                    buffer[destOffset + 1] = (byte)(255 - gByte); // M
                    buffer[destOffset + 2] = (byte)(255 - bByte); // Y
                    buffer[destOffset + 3] = k;                  // K
                    destOffset += 4;

                    accX0 += _horizontalSamplingFactor0;
                    if (accX0 >= _hMax)
                    {
                        accX0 -= _hMax;
                        sx0++;
                    }

                    accX1 += _horizontalSamplingFactor1;
                    if (accX1 >= _hMax)
                    {
                        accX1 -= _hMax;
                        sx1++;
                    }

                    accX2 += _horizontalSamplingFactor2;
                    if (accX2 >= _hMax)
                    {
                        accX2 -= _hMax;
                        sx2++;
                    }

                    accX3 += _horizontalSamplingFactor3;
                    if (accX3 >= _hMax)
                    {
                        accX3 -= _hMax;
                        sx3++;
                    }
                }

                accY0 += _verticalSamplingFactor0;
                if (accY0 >= _vMax)
                {
                    accY0 -= _vMax;
                    sampleRow0++;
                    yOffset0 = sampleRow0 * _tileWidth0;
                }

                accY1 += _verticalSamplingFactor1;
                if (accY1 >= _vMax)
                {
                    accY1 -= _vMax;
                    sampleRow1++;
                    yOffset1 = sampleRow1 * _tileWidth1;
                }

                accY2 += _verticalSamplingFactor2;
                if (accY2 >= _vMax)
                {
                    accY2 -= _vMax;
                    sampleRow2++;
                    yOffset2 = sampleRow2 * _tileWidth2;
                }

                accY3 += _verticalSamplingFactor3;
                if (accY3 >= _vMax)
                {
                    accY3 -= _vMax;
                    sampleRow3++;
                    yOffset3 = sampleRow3 * _tileWidth3;
                }
            }
        }
    }
}