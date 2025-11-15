using PdfReader.Color.Structures;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Rgb
{
    /// <summary>
    /// Upsamples a row of 4-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb4RowUpsampler : IRowUpsampler
    {
        private readonly int _columns;

        public Rgb4RowUpsampler(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Upsample(ref byte source, ref byte destination)
        {
            ref var rgbDestination = ref Unsafe.As<byte, RgbaPacked>(ref destination);

            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int sampleBase = columnIndex * 3;
                // R
                int rByteIndex = sampleBase >> 1;
                bool rHighNibble = (sampleBase & 1) == 0;
                int rValue = Unsafe.Add(ref source, rByteIndex);
                int rRaw = rHighNibble ? rValue >> 4 : rValue & 0xF;
                // G
                int gSampleIndex = sampleBase + 1;
                int gByteIndex = gSampleIndex >> 1;
                bool gHighNibble = (gSampleIndex & 1) == 0;
                int gValue = Unsafe.Add(ref source, gByteIndex);
                int gRaw = gHighNibble ? gValue >> 4 : gValue & 0xF;
                // B
                int bSampleIndex = sampleBase + 2;
                int bByteIndex = bSampleIndex >> 1;
                bool bHighNibble = (bSampleIndex & 1) == 0;
                int bValue = Unsafe.Add(ref source, bByteIndex);
                int bRaw = bHighNibble ? bValue >> 4 : bValue & 0xF;
                rgbDestination.R = (byte)(rRaw * 17);
                rgbDestination.G = (byte)(gRaw * 17);
                rgbDestination.B = (byte)(bRaw * 17);
                rgbDestination.A = 255;
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }
}
