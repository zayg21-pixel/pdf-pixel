using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Upsamples a single-component grayscale scanline from packed sample representation to 8-bit per pixel values.
    /// Input buffer contains one component per pixel encoded with the specified bits per component (1,2,4,8,16).
    /// Output buffer receives a single byte (0..255) per pixel. For sub‑8 bit depths values are scaled up to 0..255.
    /// For 16-bit samples the high byte is taken (simple downscale) matching other upsampling paths.
    /// </summary>
    internal static class PdfImageGrayUpsampler
    {
        /// <summary>
        /// Upsamples a grayscale row from packed source to 8-bit output, applying pixel processing.
        /// </summary>
        /// <param name="source">Reference to the first byte of the source buffer.</param>
        /// <param name="destination">Reference to the first byte of the destination buffer.</param>
        /// <param name="columns">Number of pixels in the row.</param>
        /// <param name="bitsPerComponent">Bits per grayscale component (1,2,4,8,16).</param>
        /// <param name="pixelProcessor">Pixel processor for per-pixel post-processing.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpsampleScaleGrayRow(
            ref byte source,
            ref byte destination,
            int columns,
            int bitsPerComponent,
            PdfPixelProcessor pixelProcessor)
        {
            switch (bitsPerComponent)
            {
                case 1:
                    UpsampleScaleGray1(ref source, ref destination, columns, pixelProcessor);
                    break;
                case 2:
                    UpsampleScaleGray2(ref source, ref destination, columns, pixelProcessor);
                    break;
                case 4:
                    UpsampleScaleGray4(ref source, ref destination, columns, pixelProcessor);
                    break;
                case 8:
                    UpsampleScaleGray8(ref source, ref destination, columns, pixelProcessor);
                    break;
                case 16:
                    UpsampleScaleGray16(ref source, ref destination, columns, pixelProcessor);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray1(
            ref byte source,
            ref byte destination,
            int columns,
            PdfPixelProcessor pixelProcessor)
        {
            // 1 bit per sample, high bit first in each source byte.
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 3; // 8 samples per byte
                int bitOffset = 7 - (pixelIndex & 7);
                int rawBit = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x1;
                byte grayValue = (byte)(rawBit * 255);
                pixelProcessor.ExecuteGray(ref grayValue);
                Unsafe.Add(ref destination, pixelIndex) = grayValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray2(
            ref byte source,
            ref byte destination,
            int columns,
            PdfPixelProcessor pixelProcessor)
        {
            // 2 bits per sample, 4 samples per byte, high bits first.
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 2; // 4 samples per byte
                int sampleInByte = pixelIndex & 3; // 0..3
                int bitOffset = 6 - (sampleInByte * 2);
                int rawValue = (Unsafe.Add(ref source, byteIndex) >> bitOffset) & 0x3; // 0..3
                byte grayValue = (byte)(rawValue * 85); // Scale 0..3 -> 0..255 (255/3 = 85)
                pixelProcessor.ExecuteGray(ref grayValue);
                Unsafe.Add(ref destination, pixelIndex) = grayValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray4(
            ref byte source,
            ref byte destination,
            int columns,
            PdfPixelProcessor pixelProcessor)
        {
            // 4 bits per sample, 2 samples per byte, high nibble first.
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 1; // 2 samples per byte
                bool highNibble = (pixelIndex & 1) == 0;
                int value = Unsafe.Add(ref source, byteIndex);
                int rawValue = highNibble ? (value >> 4) : (value & 0xF); // 0..15
                byte grayValue = (byte)(rawValue * 17); // Scale 0..15 -> 0..255 (255/15 ≈ 17)
                pixelProcessor.ExecuteGray(ref grayValue);
                Unsafe.Add(ref destination, pixelIndex) = grayValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray8(
            ref byte source,
            ref byte destination,
            int columns,
            PdfPixelProcessor pixelProcessor)
        {
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                byte grayValue = Unsafe.Add(ref source, pixelIndex);
                pixelProcessor.ExecuteGray(ref grayValue);
                Unsafe.Add(ref destination, pixelIndex) = grayValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray16(
            ref byte source,
            ref byte destination,
            int columns,
            PdfPixelProcessor pixelProcessor)
        {
            // 16 bits per sample, big-endian (high byte first). Take high byte only.
            for (int pixelIndex = 0, sourceOffset = 0; pixelIndex < columns; pixelIndex++, sourceOffset += 2)
            {
                byte grayValue = Unsafe.Add(ref source, sourceOffset);
                pixelProcessor.ExecuteGray(ref grayValue);
                Unsafe.Add(ref destination, pixelIndex) = grayValue;
            }
        }
    }
}
