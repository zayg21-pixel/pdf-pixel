using PdfReader.Color.Structures;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Sampling.Rgb
{
    /// <summary>
    /// Upsamples a row of 8-bit RGB pixels into RGBA format.
    /// </summary>
    internal sealed class Rgb8RowUpsampler : IRowUpsampler
    {
        private readonly int _columns;

        public Rgb8RowUpsampler(int columns)
        {
            _columns = columns;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Upsample(ref byte source, ref byte destination)
        {
            ref var rgbDestination = ref Unsafe.As<byte, RgbaPacked>(ref destination);
            ref RgbPacked sourcePixel = ref Unsafe.As<byte, RgbPacked>(ref source);
            for (int columnIndex = 0; columnIndex < _columns; columnIndex++)
            {
                rgbDestination = Unsafe.As<RgbPacked, RgbaPacked>(ref sourcePixel);
                rgbDestination.A = 255;
                sourcePixel = ref Unsafe.Add(ref sourcePixel, 1);
                rgbDestination = ref Unsafe.Add(ref rgbDestination, 1);
            }
        }
    }
}
