using System;
using PdfReader.Rendering.Image.Jpg.Model;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    /// <summary>
    /// MCU writer for YCbCr JPEG images.
    /// Converts Y, Cb, Cr component tiles to interleaved RGB triples (3 bytes per pixel).
    /// Sampling factors are honored with nearest-neighbor replication per MCU pixel.
    /// </summary>
    internal sealed class YCbCrMcuWriter : BaseMcuWriter
    {
        // Fixed-point scale (14 bits)
        private const int FixedPointScale = 16384;
        private const int RCrCoeff = 22970;    // 1.402 * 16384
        private const int GCbCoeff = 5638;     // 0.344136 * 16384
        private const int GCrCoeff = 11698;    // 0.714136 * 16384
        private const int BCbCoeff = 29032;    // 1.772 * 16384
        private const int RCrOffset = 179;     // (RCrCoeff * 128) >> 14
        private const int GCbCrOffset = 135;   // ((GCbCoeff * 128 + GCrCoeff * 128) >> 14)
        private const int BCbOffset = 226;     // (BCbCoeff * 128) >> 14

        private readonly byte[] _tile0; // Y
        private readonly byte[] _tile1; // Cb
        private readonly byte[] _tile2; // Cr
        private readonly int _tileWidth0;
        private readonly int _tileWidth1;
        private readonly int _tileWidth2;
        private readonly int _verticalSamplingFactor0;
        private readonly int _verticalSamplingFactor1;
        private readonly int _verticalSamplingFactor2;
        private readonly int _horizontalSamplingFactor0;
        private readonly int _horizontalSamplingFactor1;
        private readonly int _horizontalSamplingFactor2;

        public YCbCrMcuWriter(
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
                throw new ArgumentException("YCbCrMcuWriter requires exactly 3 components.", nameof(header));
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

        /// <summary>
        /// Write MCU pixels to the output buffer in RGB format using fixed-point YCbCr to RGB conversion.
        /// </summary>
        /// <param name="buffer">Output RGB buffer.</param>
        /// <param name="xBase">Starting X coordinate in the output image.</param>
        /// <param name="heightPixels">Number of pixel rows to write.</param>
        public override void WriteToBuffer(byte[] buffer, int xBase, int heightPixels)
        {
            int accY0 = 0;
            int sy0 = 0;
            int yOff0 = 0;
            int accY1 = 0;
            int sy1 = 0;
            int yOff1 = 0;
            int accY2 = 0;
            int sy2 = 0;
            int yOff2 = 0;

            for (int localRow = 0; localRow < heightPixels; localRow++)
            {
                int destRowOffset = localRow * _outputStride;
                int destOffset = destRowOffset + xBase * 3;

                int accX0 = 0;
                int sx0 = 0;
                int accX1 = 0;
                int sx1 = 0;
                int accX2 = 0;
                int sx2 = 0;

                for (int localX = 0; localX < _mcuWidth && xBase + localX < _imageWidth; localX++)
                {
                    byte y = _tile0[yOff0 + sx0];
                    byte cb = _tile1[yOff1 + sx1];
                    byte cr = _tile2[yOff2 + sx2];

                    int r = y + ((RCrCoeff * cr) >> 14) - RCrOffset;
                    int g = y - ((GCbCoeff * cb + GCrCoeff * cr) >> 14) + GCbCrOffset;
                    int b = y + ((BCbCoeff * cb) >> 14) - BCbOffset;

                    buffer[destOffset + 0] = r < 0 ? (byte)0 : r > 255 ? (byte)255 : (byte)r;
                    buffer[destOffset + 1] = g < 0 ? (byte)0 : g > 255 ? (byte)255 : (byte)g;
                    buffer[destOffset + 2] = b < 0 ? (byte)0 : b > 255 ? (byte)255 : (byte)b;
                    destOffset += 3;

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
                }

                accY0 += _verticalSamplingFactor0;
                if (accY0 >= _vMax)
                {
                    accY0 -= _vMax;
                    sy0++;
                    yOff0 = sy0 * _tileWidth0;
                }

                accY1 += _verticalSamplingFactor1;
                if (accY1 >= _vMax)
                {
                    accY1 -= _vMax;
                    sy1++;
                    yOff1 = sy1 * _tileWidth1;
                }

                accY2 += _verticalSamplingFactor2;
                if (accY2 >= _vMax)
                {
                    accY2 -= _vMax;
                    sy2++;
                    yOff2 = sy2 * _tileWidth2;
                }
            }
        }
    }
}