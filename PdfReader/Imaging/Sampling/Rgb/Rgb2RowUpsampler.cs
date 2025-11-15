using PdfReader.Rendering.Color.Clut;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Rgb
{
    /// <summary>
    /// Upsamples a row of 2-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb2RowUpsampler : IRowUpsampler
    {
        private readonly int _columns;

        public Rgb2RowUpsampler(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Upsample(ref byte source, ref byte destination)
        {
            ref var rgbDestination = ref Unsafe.As<byte, Rgba>(ref destination);

            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                int sampleBase = columnIndex * 3;
                // R
                int rByteIndex = sampleBase >> 2;
                int rTwoBitIndex = sampleBase & 3;
                int rBitOffset = 6 - rTwoBitIndex * 2;
                int rRaw = Unsafe.Add(ref source, rByteIndex) >> rBitOffset & 0x3;
                // G
                int gSampleIndex = sampleBase + 1;
                int gByteIndex = gSampleIndex >> 2;
                int gTwoBitIndex = gSampleIndex & 3;
                int gBitOffset = 6 - gTwoBitIndex * 2;
                int gRaw = Unsafe.Add(ref source, gByteIndex) >> gBitOffset & 0x3;
                // B
                int bSampleIndex = sampleBase + 2;
                int bByteIndex = bSampleIndex >> 2;
                int bTwoBitIndex = bSampleIndex & 3;
                int bBitOffset = 6 - bTwoBitIndex * 2;
                int bRaw = Unsafe.Add(ref source, bByteIndex) >> bBitOffset & 0x3;
                rgbDestination.R = (byte)(rRaw * 85);
                rgbDestination.G = (byte)(gRaw * 85);
                rgbDestination.B = (byte)(bRaw * 85);
                rgbDestination.A = 255;
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }
}
