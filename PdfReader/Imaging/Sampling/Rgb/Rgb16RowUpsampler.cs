using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Rgb
{
    /// <summary>
    /// Upsamples a row of 16-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb16RowUpsampler : IRowUpsampler
    {
        private readonly int _columns;

        public Rgb16RowUpsampler(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Upsample(ref byte source, ref byte destination)
        {
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int srcOffset = columnIndex * 3 * 2;
                int dstOffset = columnIndex * 4 * 2;

                ushort r = (ushort)(Unsafe.Add(ref source, srcOffset) | Unsafe.Add(ref source, srcOffset + 1) << 8);
                ushort g = (ushort)(Unsafe.Add(ref source, srcOffset + 2) | Unsafe.Add(ref source, srcOffset + 3) << 8);
                ushort b = (ushort)(Unsafe.Add(ref source, srcOffset + 4) | Unsafe.Add(ref source, srcOffset + 5) << 8);
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
