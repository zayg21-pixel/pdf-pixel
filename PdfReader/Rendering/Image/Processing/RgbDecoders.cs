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
        private readonly PdfPixelProcessor _processor;

        public Rgb1RowDecoder(int columns, PdfPixelProcessor processor)
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
                destination.R = (byte)(rRaw * 255);
                destination.G = (byte)(gRaw * 255);
                destination.B = (byte)(bRaw * 255);
                destination.A = 255;
                _processor?.ExecuteRgba(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 2-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb2RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public Rgb2RowDecoder(int columns, PdfPixelProcessor processor)
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
                destination.R = (byte)(rRaw * 85);
                destination.G = (byte)(gRaw * 85);
                destination.B = (byte)(bRaw * 85);
                destination.A = 255;
                _processor?.ExecuteRgba(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 4-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb4RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public Rgb4RowDecoder(int columns, PdfPixelProcessor processor)
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
                destination.R = (byte)(rRaw * 17);
                destination.G = (byte)(gRaw * 17);
                destination.B = (byte)(bRaw * 17);
                destination.A = 255;
                _processor?.ExecuteRgba(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 8-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb8RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb8RowDecoder"/> class.
        /// </summary>
        /// <param name="columns">The number of pixels (columns) in the row.</param>
        /// <param name="processor">The pixel processor to apply color conversion and post-processing.</param>
        public Rgb8RowDecoder(int columns, PdfPixelProcessor processor)
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
            ref Rgb sourcePixel = ref Unsafe.As<byte, Rgb>(ref source);
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                destination = Unsafe.As<Rgb, Rgba>(ref sourcePixel);
                destination.A = 255;
                _processor?.ExecuteRgba(ref destination);
                sourcePixel = ref Unsafe.Add(ref sourcePixel, 1);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 16-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb16RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public Rgb16RowDecoder(int columns, PdfPixelProcessor processor)
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
            for (int columnIndex = 0, sourceOffset = 0; columnIndex < _columns; columnIndex++, sourceOffset += 6)
            {
                int r16 = (Unsafe.Add(ref source, sourceOffset) << 8) | Unsafe.Add(ref source, sourceOffset + 1);
                int g16 = (Unsafe.Add(ref source, sourceOffset + 2) << 8) | Unsafe.Add(ref source, sourceOffset + 3);
                int b16 = (Unsafe.Add(ref source, sourceOffset + 4) << 8) | Unsafe.Add(ref source, sourceOffset + 5);
                destination.R = (byte)(r16 >> 8);
                destination.G = (byte)(g16 >> 8);
                destination.B = (byte)(b16 >> 8);
                destination.A = 255;
                _processor?.ExecuteRgba(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }
}
