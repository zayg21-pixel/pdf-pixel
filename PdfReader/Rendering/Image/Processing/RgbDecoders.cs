using PdfReader.Rendering.Color.Clut;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Decodes a row of 1-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb1RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public Rgb1RowDecoder(int columns)
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
                int sampleBase = columnIndex * 3;
                // R
                int rByteIndex = sampleBase >> 3;
                int rBitOffset = 7 - (sampleBase & 7);
                int rRaw = (Unsafe.Add(ref source, rByteIndex) >> rBitOffset) & 0x1;
                // G
                int gSampleIndex = sampleBase + 1;
                int gByteIndex = gSampleIndex >> 3;
                int gBitOffset = 7 - (gSampleIndex & 7);
                int gRaw = (Unsafe.Add(ref source, gByteIndex) >> gBitOffset) & 0x1;
                // B
                int bSampleIndex = sampleBase + 2;
                int bByteIndex = bSampleIndex >> 3;
                int bBitOffset = 7 - (bSampleIndex & 7);
                int bRaw = (Unsafe.Add(ref source, bByteIndex) >> bBitOffset) & 0x1;
                rgbDestination.R = (byte)(rRaw * 255);
                rgbDestination.G = (byte)(gRaw * 255);
                rgbDestination.B = (byte)(bRaw * 255);
                rgbDestination.A = 255;
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 2-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb2RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public Rgb2RowDecoder(int columns)
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
                int sampleBase = columnIndex * 3;
                // R
                int rByteIndex = sampleBase >> 2;
                int rTwoBitIndex = sampleBase & 3;
                int rBitOffset = 6 - (rTwoBitIndex * 2);
                int rRaw = (Unsafe.Add(ref source, rByteIndex) >> rBitOffset) & 0x3;
                // G
                int gSampleIndex = sampleBase + 1;
                int gByteIndex = gSampleIndex >> 2;
                int gTwoBitIndex = gSampleIndex & 3;
                int gBitOffset = 6 - (gTwoBitIndex * 2);
                int gRaw = (Unsafe.Add(ref source, gByteIndex) >> gBitOffset) & 0x3;
                // B
                int bSampleIndex = sampleBase + 2;
                int bByteIndex = bSampleIndex >> 2;
                int bTwoBitIndex = bSampleIndex & 3;
                int bBitOffset = 6 - (bTwoBitIndex * 2);
                int bRaw = (Unsafe.Add(ref source, bByteIndex) >> bBitOffset) & 0x3;
                rgbDestination.R = (byte)(rRaw * 85);
                rgbDestination.G = (byte)(gRaw * 85);
                rgbDestination.B = (byte)(bRaw * 85);
                rgbDestination.A = 255;
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 4-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb4RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public Rgb4RowDecoder(int columns)
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
                int sampleBase = columnIndex * 3;
                // R
                int rByteIndex = sampleBase >> 1;
                bool rHighNibble = (sampleBase & 1) == 0;
                int rValue = Unsafe.Add(ref source, rByteIndex);
                int rRaw = rHighNibble ? (rValue >> 4) : (rValue & 0xF);
                // G
                int gSampleIndex = sampleBase + 1;
                int gByteIndex = gSampleIndex >> 1;
                bool gHighNibble = (gSampleIndex & 1) == 0;
                int gValue = Unsafe.Add(ref source, gByteIndex);
                int gRaw = gHighNibble ? (gValue >> 4) : (gValue & 0xF);
                // B
                int bSampleIndex = sampleBase + 2;
                int bByteIndex = bSampleIndex >> 1;
                bool bHighNibble = (bSampleIndex & 1) == 0;
                int bValue = Unsafe.Add(ref source, bByteIndex);
                int bRaw = bHighNibble ? (bValue >> 4) : (bValue & 0xF);
                rgbDestination.R = (byte)(rRaw * 17);
                rgbDestination.G = (byte)(gRaw * 17);
                rgbDestination.B = (byte)(bRaw * 17);
                rgbDestination.A = 255;
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 8-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb8RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public Rgb8RowDecoder(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            ref var rgbDestination = ref Unsafe.As<byte, Rgba>(ref destination);
            ref Rgb sourcePixel = ref Unsafe.As<byte, Rgb>(ref source);
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                rgbDestination = Unsafe.As<Rgb, Rgba>(ref sourcePixel);
                rgbDestination.A = 255;
                sourcePixel = ref Unsafe.Add(ref sourcePixel, 1);
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 16-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb16RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public Rgb16RowDecoder(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int srcOffset = columnIndex * 3 * 2;
                int dstOffset = columnIndex * 4 * 2;

                ushort r = (ushort)(Unsafe.Add(ref source, srcOffset) | (Unsafe.Add(ref source, srcOffset + 1) << 8));
                ushort g = (ushort)(Unsafe.Add(ref source, srcOffset + 2) | (Unsafe.Add(ref source, srcOffset + 3) << 8));
                ushort b = (ushort)(Unsafe.Add(ref source, srcOffset + 4) | (Unsafe.Add(ref source, srcOffset + 5) << 8));
                ushort a = 65535;

                Unsafe.Add(ref destination, dstOffset) = (byte)(r & 0xFF);
                Unsafe.Add(ref destination, dstOffset + 1) = (byte)(r >> 8);
                Unsafe.Add(ref destination, dstOffset + 2) = (byte)(g & 0xFF);
                Unsafe.Add(ref destination, dstOffset + 3) = (byte)(g >> 8);
                Unsafe.Add(ref destination, dstOffset + 4) = (byte)(b & 0xFF);
                Unsafe.Add(ref destination, dstOffset + 5) = (byte)(b >> 8);
                Unsafe.Add(ref destination, dstOffset + 6) = (byte)(a & 0xFF);
                Unsafe.Add(ref destination, dstOffset + 7) = (byte)(a >> 8);
            }
        }
    }
}
