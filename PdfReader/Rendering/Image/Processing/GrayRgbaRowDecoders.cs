using PdfReader.Rendering.Color.Clut;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Decodes a row of 1-bit grayscale pixels into RGBA format.
    /// </summary>
    internal sealed class GrayRgba1RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public GrayRgba1RowDecoder(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            ref var rgbDestination = ref Unsafe.As<byte, Rgba>(ref destination);

            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int byteIndex = columnIndex >> 3;
                int bitOffset = 7 - (columnIndex & 7);
                int rawBit = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x1;
                byte gray = (byte)(rawBit * 255);
                rgbDestination.R = gray;
                rgbDestination.G = gray;
                rgbDestination.B = gray;
                rgbDestination.A = 255;

                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 2-bit grayscale pixels into RGBA format.
    /// </summary>
    internal sealed class GrayRgba2RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public GrayRgba2RowDecoder(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            ref var rgbDestination = ref Unsafe.As<byte, Rgba>(ref destination);

            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int byteIndex = columnIndex >> 2;
                int sampleInByte = columnIndex & 3;
                int bitOffset = 6 - (sampleInByte * 2);
                int rawValue = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x3;
                byte gray = (byte)(rawValue * 85);
                rgbDestination.R = gray;
                rgbDestination.G = gray;
                rgbDestination.B = gray;
                rgbDestination.A = 255;

                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 4-bit grayscale pixels into RGBA format.
    /// </summary>
    internal sealed class GrayRgba4RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private const int NibbleScaleFactor = 17;

        public GrayRgba4RowDecoder(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            ref var rgbDestination = ref Unsafe.As<byte, Rgba>(ref destination);

            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int byteIndex = columnIndex >> 1;
                bool isHighNibble = (columnIndex & 1) == 0;
                int value = Unsafe.Add(ref source, byteIndex);
                int rawValue = isHighNibble ? (value >> 4) : (value & 0xF);
                byte gray = (byte)(rawValue * NibbleScaleFactor);
                rgbDestination.R = gray;
                rgbDestination.G = gray;
                rgbDestination.B = gray;
                rgbDestination.A = 255;
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 8-bit grayscale pixels into RGBA format.
    /// </summary>
    internal sealed class GrayRgba8RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public GrayRgba8RowDecoder(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            ref var rgbDestination = ref Unsafe.As<byte, Rgba>(ref destination);

            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                byte gray = Unsafe.Add(ref source, columnIndex);
                rgbDestination.R = gray;
                rgbDestination.G = gray;
                rgbDestination.B = gray;
                rgbDestination.A = 255;
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 16-bit grayscale pixels into RGBA format (writes full 16 bits per channel).
    /// </summary>
    internal sealed class GrayRgba16RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public GrayRgba16RowDecoder(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int srcOffset = columnIndex * 2;
                int dstOffset = columnIndex * 4 * 2;

                ushort gray = (ushort)(Unsafe.Add(ref source, srcOffset) | (Unsafe.Add(ref source, srcOffset + 1) << 8));
                ushort alpha = 65535;

                Unsafe.Add(ref destination, dstOffset) = (byte)(gray & 0xFF);         // R low
                Unsafe.Add(ref destination, dstOffset + 1) = (byte)(gray >> 8);      // R high
                Unsafe.Add(ref destination, dstOffset + 2) = (byte)(gray & 0xFF);    // G low
                Unsafe.Add(ref destination, dstOffset + 3) = (byte)(gray >> 8);      // G high
                Unsafe.Add(ref destination, dstOffset + 4) = (byte)(gray & 0xFF);    // B low
                Unsafe.Add(ref destination, dstOffset + 5) = (byte)(gray >> 8);      // B high
                Unsafe.Add(ref destination, dstOffset + 6) = (byte)(alpha & 0xFF);   // A low
                Unsafe.Add(ref destination, dstOffset + 7) = (byte)(alpha >> 8);     // A high
            }
        }
    }
}
