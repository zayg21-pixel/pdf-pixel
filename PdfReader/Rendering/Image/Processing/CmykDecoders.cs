using PdfReader.Rendering.Color.Clut;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Decodes a row of 1-bit CMYK pixels into RGBA format.
    /// </summary>
    internal sealed class Cmyk1RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public Cmyk1RowDecoder(int columns)
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
                int sampleBase = columnIndex * 4;
                int cByteIndex = sampleBase >> 3;
                int cBitOffset = 7 - (sampleBase & 7);
                int cRaw = (Unsafe.Add(ref source, cByteIndex) >> cBitOffset) & 0x1;
                int mSampleIndex = sampleBase + 1;
                int mByteIndex = mSampleIndex >> 3;
                int mBitOffset = 7 - (mSampleIndex & 7);
                int mRaw = (Unsafe.Add(ref source, mByteIndex) >> mBitOffset) & 0x1;
                int ySampleIndex = sampleBase + 2;
                int yByteIndex = ySampleIndex >> 3;
                int yBitOffset = 7 - (ySampleIndex & 7);
                int yRaw = (Unsafe.Add(ref source, yByteIndex) >> yBitOffset) & 0x1;
                int kSampleIndex = sampleBase + 3;
                int kByteIndex = kSampleIndex >> 3;
                int kBitOffset = 7 - (kSampleIndex & 7);
                int kRaw = (Unsafe.Add(ref source, kByteIndex) >> kBitOffset) & 0x1;
                rgbDestination.R = (byte)(cRaw * 255);
                rgbDestination.G = (byte)(mRaw * 255);
                rgbDestination.B = (byte)(yRaw * 255);
                rgbDestination.A = (byte)(kRaw * 255);
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 2-bit CMYK pixels into RGBA format.
    /// </summary>
    internal sealed class Cmyk2RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public Cmyk2RowDecoder(int columns)
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
                int sampleBase = columnIndex * 4;
                int cByteIndex = sampleBase >> 2;
                int cTwoBitIndex = sampleBase & 3;
                int cBitOffset = 6 - (cTwoBitIndex * 2);
                int cRaw = (Unsafe.Add(ref source, cByteIndex) >> cBitOffset) & 0x3;
                int mSampleIndex = sampleBase + 1;
                int mByteIndex = mSampleIndex >> 2;
                int mTwoBitIndex = mSampleIndex & 3;
                int mBitOffset = 6 - (mTwoBitIndex * 2);
                int mRaw = (Unsafe.Add(ref source, mByteIndex) >> mBitOffset) & 0x3;
                int ySampleIndex = sampleBase + 2;
                int yByteIndex = ySampleIndex >> 2;
                int yTwoBitIndex = ySampleIndex & 3;
                int yBitOffset = 6 - (yTwoBitIndex * 2);
                int yRaw = (Unsafe.Add(ref source, yByteIndex) >> yBitOffset) & 0x3;
                int kSampleIndex = sampleBase + 3;
                int kByteIndex = kSampleIndex >> 2;
                int kTwoBitIndex = kSampleIndex & 3;
                int kBitOffset = 6 - (kTwoBitIndex * 2);
                int kRaw = (Unsafe.Add(ref source, kByteIndex) >> kBitOffset) & 0x3;
                rgbDestination.R = (byte)(cRaw * 85);
                rgbDestination.G = (byte)(mRaw * 85);
                rgbDestination.B = (byte)(yRaw * 85);
                rgbDestination.A = (byte)(kRaw * 85);
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 4-bit CMYK pixels into RGBA format.
    /// </summary>
    internal sealed class Cmyk4RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public Cmyk4RowDecoder(int columns)
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
                int sampleBase = columnIndex * 4;
                int cByteIndex = sampleBase >> 1;
                bool cHigh = (sampleBase & 1) == 0;
                int cValue = Unsafe.Add(ref source, cByteIndex);
                int cRaw = cHigh ? (cValue >> 4) : (cValue & 0xF);
                int mSampleIndex = sampleBase + 1;
                int mByteIndex = mSampleIndex >> 1;
                bool mHigh = (mSampleIndex & 1) == 0;
                int mValue = Unsafe.Add(ref source, mByteIndex);
                int mRaw = mHigh ? (mValue >> 4) : (mValue & 0xF);
                int ySampleIndex = sampleBase + 2;
                int yByteIndex = ySampleIndex >> 1;
                bool yHigh = (ySampleIndex & 1) == 0;
                int yValue = Unsafe.Add(ref source, yByteIndex);
                int yRaw = yHigh ? (yValue >> 4) : (yValue & 0xF);
                int kSampleIndex = sampleBase + 3;
                int kByteIndex = kSampleIndex >> 1;
                bool kHigh = (kSampleIndex & 1) == 0;
                int kValue = Unsafe.Add(ref source, kByteIndex);
                int kRaw = kHigh ? (kValue >> 4) : (kValue & 0xF);
                rgbDestination.R = (byte)(cRaw * 17);
                rgbDestination.G = (byte)(mRaw * 17);
                rgbDestination.B = (byte)(yRaw * 17);
                rgbDestination.A = (byte)(kRaw * 17);
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 8-bit CMYK pixels into RGBA format.
    /// </summary>
    internal sealed class Cmyk8RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public Cmyk8RowDecoder(int columns)
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
                uint value = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, columnIndex * 4));
                rgbDestination = Unsafe.As<uint, Rgba>(ref value);
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 16-bit CMYK pixels into RGBA format.
    /// </summary>
    internal sealed class Cmyk16RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;

        public Cmyk16RowDecoder(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref byte destination)
        {
            // Writes raw 16-bit CMYK values for each pixel, allowing later color management (e.g., ICC profile conversion).
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int sourceOffset = columnIndex * 4 * 2; // 4 components, 2 bytes each
                int destinationOffset = columnIndex * 4 * 2; // 4 components, 2 bytes each

                ushort cyan = (ushort)(Unsafe.Add(ref source, sourceOffset) | (Unsafe.Add(ref source, sourceOffset + 1) << 8));
                ushort magenta = (ushort)(Unsafe.Add(ref source, sourceOffset + 2) | (Unsafe.Add(ref source, sourceOffset + 3) << 8));
                ushort yellow = (ushort)(Unsafe.Add(ref source, sourceOffset + 4) | (Unsafe.Add(ref source, sourceOffset + 5) << 8));
                ushort black = (ushort)(Unsafe.Add(ref source, sourceOffset + 6) | (Unsafe.Add(ref source, sourceOffset + 7) << 8));

                Unsafe.Add(ref destination, destinationOffset) = (byte)(cyan & 0xFF);
                Unsafe.Add(ref destination, destinationOffset + 1) = (byte)(cyan >> 8);
                Unsafe.Add(ref destination, destinationOffset + 2) = (byte)(magenta & 0xFF);
                Unsafe.Add(ref destination, destinationOffset + 3) = (byte)(magenta >> 8);
                Unsafe.Add(ref destination, destinationOffset + 4) = (byte)(yellow & 0xFF);
                Unsafe.Add(ref destination, destinationOffset + 5) = (byte)(yellow >> 8);
                Unsafe.Add(ref destination, destinationOffset + 6) = (byte)(black & 0xFF);
                Unsafe.Add(ref destination, destinationOffset + 7) = (byte)(black >> 8);
            }
        }
    }
}
