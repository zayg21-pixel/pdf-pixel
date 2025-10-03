using System;
using PdfReader.Rendering.Image.Jpg.Model;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    /// <summary>
    /// Base class for MCU writers providing common functionality and sampling logic.
    /// </summary>
    internal abstract class BaseMcuWriter : IMcuWriter
    {
        protected readonly JpgHeader _header;
        protected readonly byte[][] _componentTiles;
        protected readonly int[] _tileWidths;
        protected readonly int _hMax;
        protected readonly int _vMax;
        protected readonly int _mcuWidth;
        protected readonly int _imageWidth;
        protected readonly int _outputStride; // bytes per row = width * componentCount
        protected readonly int _componentCount;

        protected BaseMcuWriter(
            JpgHeader header,
            byte[][] componentTiles,
            int[] tileWidths,
            int hMax,
            int vMax,
            int mcuWidth,
            int imageWidth,
            int outputStride)
        {
            _header = header ?? throw new ArgumentNullException(nameof(header));
            _componentTiles = componentTiles ?? throw new ArgumentNullException(nameof(componentTiles));
            _tileWidths = tileWidths ?? throw new ArgumentNullException(nameof(tileWidths));
            _hMax = hMax;
            _vMax = vMax;
            _mcuWidth = mcuWidth;
            _imageWidth = imageWidth;
            _outputStride = outputStride;
            _componentCount = header.ComponentCount;
        }

        public abstract void WriteToBuffer(byte[] buffer, int xBase, int heightPixels);

        /// <summary>
        /// Clamp an integer value to byte range (0-255).
        /// </summary>
        protected static byte ClampToByte(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 255)
            {
                return 255;
            }

            return (byte)value;
        }
    }
}