using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Decodes a row of 1-bit grayscale pixels into 8-bit grayscale values.
    /// </summary>
    internal sealed class Gray1RowDecoder : IGrayRowDecoder
    {
        private readonly int _columns;
        private readonly bool _upscale;
        private readonly int _scaleFactor;

        public Gray1RowDecoder(int columns, bool upscale)
        {
            _columns = columns;
            _upscale = upscale;
            _scaleFactor = _upscale ? 255 : 1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            for (int pixelIndex = 0; pixelIndex < _columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 3;
                int bitOffset = 7 - (pixelIndex & 7);
                int rawBit = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x1;
                byte grayValue = (byte)(rawBit * _scaleFactor);
                Unsafe.Add(ref destination, pixelIndex) = grayValue;
            }
        }
    }

    /// <summary>
    /// Decodes a row of 2-bit grayscale pixels into 8-bit grayscale values.
    /// </summary>
    internal sealed class Gray2RowDecoder : IGrayRowDecoder
    {
        private readonly int _columns;
        private readonly bool _upscale;
        private readonly int _scaleFactor;

        public Gray2RowDecoder(int columns, bool upscale)
        {
            _columns = columns;
            _upscale = upscale;
            _scaleFactor = _upscale ? 85 : 1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            for (int pixelIndex = 0; pixelIndex < _columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 2;
                int sampleInByte = pixelIndex & 3;
                int bitOffset = 6 - (sampleInByte * 2);
                int rawValue = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x3;
                byte grayValue = (byte)(rawValue * _scaleFactor);
                Unsafe.Add(ref destination, pixelIndex) = grayValue;
            }
        }
    }

    /// <summary>
    /// Decodes a row of 4-bit grayscale pixels into 8-bit grayscale values.
    /// </summary>
    internal sealed class Gray4RowDecoder : IGrayRowDecoder
    {
        private readonly int _columns;
        private readonly bool _upscale;
        private readonly int _scaleFactor;

        public Gray4RowDecoder(int columns, bool upscale)
        {
            _columns = columns;
            _upscale = upscale;
            _scaleFactor = _upscale ? 17 : 1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            for (int pixelIndex = 0; pixelIndex < _columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 1;
                bool highNibble = (pixelIndex & 1) == 0;
                int value = Unsafe.Add(ref source, byteIndex);
                int rawValue = highNibble ? (value >> 4) : (value & 0xF);
                byte grayValue = (byte)(rawValue * _scaleFactor);
                Unsafe.Add(ref destination, pixelIndex) = grayValue;
            }
        }
    }

    /// <summary>
    /// Decodes a row of 8-bit grayscale pixels into 8-bit grayscale values.
    /// </summary>
    internal sealed class Gray8RowDecoder : IGrayRowDecoder
    {
        private readonly int _columns;

        public Gray8RowDecoder(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            for (int pixelIndex = 0; pixelIndex < _columns; pixelIndex++)
            {
                byte grayValue = Unsafe.Add(ref source, pixelIndex);
                Unsafe.Add(ref destination, pixelIndex) = grayValue;
            }
        }
    }

    /// <summary>
    /// Decodes a row of 16-bit grayscale pixels into 8-bit grayscale values (high byte only).
    /// </summary>
    internal sealed class Gray16RowDecoder : IGrayRowDecoder
    {
        private readonly int _columns;

        public Gray16RowDecoder(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            for (int pixelIndex = 0, sourceOffset = 0; pixelIndex < _columns; pixelIndex++, sourceOffset += 2)
            {
                byte grayValue = Unsafe.Add(ref source, sourceOffset);
                Unsafe.Add(ref destination, pixelIndex) = grayValue;
            }
        }
    }
}
