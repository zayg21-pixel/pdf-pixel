using PdfReader.Rendering.Color.Clut;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Decodes a row of 1-bit CMYK pixels into RGBA format.
    /// </summary>
    internal sealed class Cmyk1RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public Cmyk1RowDecoder(int columns, PdfPixelProcessor processor)
        {
            _columns = columns;

            if (processor.HasProcessing)
            {
                _processor = processor;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref Rgba destination)
        {
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
                destination.R = (byte)(cRaw * 255);
                destination.G = (byte)(mRaw * 255);
                destination.B = (byte)(yRaw * 255);
                destination.A = (byte)(kRaw * 255);
                _processor?.ExecuteCmyk(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 2-bit CMYK pixels into RGBA format.
    /// </summary>
    internal sealed class Cmyk2RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public Cmyk2RowDecoder(int columns, PdfPixelProcessor processor)
        {
            _columns = columns;

            if (processor.HasProcessing)
            {
                _processor = processor;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref Rgba destination)
        {
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
                destination.R = (byte)(cRaw * 85);
                destination.G = (byte)(mRaw * 85);
                destination.B = (byte)(yRaw * 85);
                destination.A = (byte)(kRaw * 85);
                _processor?.ExecuteCmyk(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 4-bit CMYK pixels into RGBA format.
    /// </summary>
    internal sealed class Cmyk4RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public Cmyk4RowDecoder(int columns, PdfPixelProcessor processor)
        {
            _columns = columns;

            if (processor.HasProcessing)
            {
                _processor = processor;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref Rgba destination)
        {
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
                destination.R = (byte)(cRaw * 17);
                destination.G = (byte)(mRaw * 17);
                destination.B = (byte)(yRaw * 17);
                destination.A = (byte)(kRaw * 17);
                _processor?.ExecuteCmyk(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 8-bit CMYK pixels into RGBA format.
    /// </summary>
    internal sealed class Cmyk8RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public Cmyk8RowDecoder(int columns, PdfPixelProcessor processor)
        {
            _columns = columns;

            if (processor.HasProcessing)
            {
                _processor = processor;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref Rgba destination)
        {
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                uint value = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, columnIndex * 4));
                destination = Unsafe.As<uint, Rgba>(ref value);
                _processor?.ExecuteCmyk(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 16-bit CMYK pixels into RGBA format.
    /// </summary>
    internal sealed class Cmyk16RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public Cmyk16RowDecoder(int columns, PdfPixelProcessor processor)
        {
            _columns = columns;

            if (processor.HasProcessing)
            {
                _processor = processor;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref Rgba destination)
        {
            for (int columnIndex = 0, sourceOffset = 0; columnIndex < _columns; columnIndex++, sourceOffset += 8)
            {
                byte cHi = Unsafe.Add(ref source, sourceOffset);
                byte mHi = Unsafe.Add(ref source, sourceOffset + 2);
                byte yHi = Unsafe.Add(ref source, sourceOffset + 4);
                byte kHi = Unsafe.Add(ref source, sourceOffset + 6);
                destination.R = cHi;
                destination.G = mHi;
                destination.B = yHi;
                destination.A = kHi;
                _processor?.ExecuteCmyk(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }
}
