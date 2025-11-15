using PdfReader.Color.Structures;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Rgb
{
    /// <summary>
    /// Upsamples a row of 1-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb1RowUpsampler : IRowUpsampler
    {
        private readonly int _columns;

        public Rgb1RowUpsampler(int columns)
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
                int rByteIndex = sampleBase >> 3;
                int rBitOffset = 7 - (sampleBase & 7);
                int rRaw = Unsafe.Add(ref source, rByteIndex) >> rBitOffset & 0x1;
                // G
                int gSampleIndex = sampleBase + 1;
                int gByteIndex = gSampleIndex >> 3;
                int gBitOffset = 7 - (gSampleIndex & 7);
                int gRaw = Unsafe.Add(ref source, gByteIndex) >> gBitOffset & 0x1;
                // B
                int bSampleIndex = sampleBase + 2;
                int bByteIndex = bSampleIndex >> 3;
                int bBitOffset = 7 - (bSampleIndex & 7);
                int bRaw = Unsafe.Add(ref source, bByteIndex) >> bBitOffset & 0x1;
                rgbDestination.R = (byte)(rRaw * 255);
                rgbDestination.G = (byte)(gRaw * 255);
                rgbDestination.B = (byte)(bRaw * 255);
                rgbDestination.A = 255;
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }
}
