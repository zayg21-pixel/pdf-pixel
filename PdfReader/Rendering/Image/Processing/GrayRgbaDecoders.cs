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
        private readonly PdfPixelProcessor _processor;

        public GrayRgba1RowDecoder(int columns, PdfPixelProcessor processor)
        {
            _columns = columns;
            _processor = processor;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref Rgba destination)
        {
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int byteIndex = columnIndex >> 3;
                int bitOffset = 7 - (columnIndex & 7);
                int rawBit = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x1;
                byte gray = (byte)(rawBit * 255);
                destination.R = gray;
                destination.G = gray;
                destination.B = gray;
                destination.A = 255;
                _processor.ExecuteGray(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 2-bit grayscale pixels into RGBA format.
    /// </summary>
    internal sealed class GrayRgba2RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public GrayRgba2RowDecoder(int columns, PdfPixelProcessor processor)
        {
            _columns = columns;
            _processor = processor;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref Rgba destination)
        {
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int byteIndex = columnIndex >> 2;
                int sampleInByte = columnIndex & 3;
                int bitOffset = 6 - (sampleInByte * 2);
                int rawValue = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x3;
                byte gray = (byte)(rawValue * 85);
                destination.R = gray;
                destination.G = gray;
                destination.B = gray;
                destination.A = 255;
                _processor.ExecuteGray(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 4-bit grayscale pixels into RGBA format.
    /// </summary>
    internal sealed class GrayRgba4RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public GrayRgba4RowDecoder(int columns, PdfPixelProcessor processor)
        {
            _columns = columns;
            _processor = processor;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref Rgba destination)
        {
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int byteIndex = columnIndex >> 1;
                bool highNibble = (columnIndex & 1) == 0;
                int value = Unsafe.Add(ref source, byteIndex);
                int rawValue = highNibble ? (value >> 4) : (value & 0xF);
                byte gray = (byte)(rawValue * 17);
                destination.R = gray;
                destination.G = gray;
                destination.B = gray;
                destination.A = 255;
                _processor.ExecuteGray(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 8-bit grayscale pixels into RGBA format.
    /// </summary>
    internal sealed class GrayRgba8RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public GrayRgba8RowDecoder(int columns, PdfPixelProcessor processor)
        {
            _columns = columns;
            _processor = processor;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref Rgba destination)
        {
            ref byte sourceByte = ref source;
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                byte gray = sourceByte;
                destination.R = gray;
                destination.G = gray;
                destination.B = gray;
                destination.A = 255;
                _processor.ExecuteGray(ref destination);
                sourceByte = ref Unsafe.Add(ref sourceByte, 1);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }

    /// <summary>
    /// Decodes a row of 16-bit grayscale pixels into RGBA format.
    /// </summary>
    internal sealed class GrayRgba16RowDecoder : IRgbaRowDecoder
    {
        private readonly int _columns;
        private readonly PdfPixelProcessor _processor;

        public GrayRgba16RowDecoder(int columns, PdfPixelProcessor processor)
        {
            _columns = columns;
            _processor = processor;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decode(ref byte source, ref Rgba destination)
        {
            for (int columnIndex = 0, sourceOffset = 0; columnIndex < _columns; columnIndex++, sourceOffset += 2)
            {
                byte highByte = Unsafe.Add(ref source, sourceOffset);
                destination.R = highByte;
                destination.G = highByte;
                destination.B = highByte;
                destination.A = 255;
                _processor.ExecuteGray(ref destination);
                destination = ref Unsafe.Add(ref destination, 1);
            }
        }
    }
}
