using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Upsamples a single-component grayscale scanline from packed sample representation to 8-bit per pixel values.
    /// Input buffer contains one component per pixel encoded with the specified bits per component (1,2,4,8,16).
    /// Output buffer receives a single byte (0..255) per pixel. For sub‑8 bit depths values are scaled up to 0..255.
    /// For 16-bit samples the high byte is taken (simple downscale) matching other upsampling paths.
    /// </summary>
    internal static unsafe class PdfImageGrayUpsampler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpsampleScaleGrayRow(byte* source, byte* destination, int columns, int bitsPerComponent)
        {
            switch (bitsPerComponent)
            {
                case 1:
                    UpsampleScaleGray1(source, destination, columns);
                    break;
                case 2:
                    UpsampleScaleGray2(source, destination, columns);
                    break;
                case 4:
                    UpsampleScaleGray4(source, destination, columns);
                    break;
                case 8:
                    UpsampleScaleGray8(source, destination, columns);
                    break;
                case 16:
                    UpsampleScaleGray16(source, destination, columns);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray1(byte* source, byte* destination, int columns)
        {
            // 1 bit per sample, high bit first in each source byte.
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 3; // 8 samples per byte
                int bitOffset = 7 - (pixelIndex & 7);
                int rawBit = (source[byteIndex] >> bitOffset) & 0x1;
                destination[pixelIndex] = (byte)(rawBit * 255);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray2(byte* source, byte* destination, int columns)
        {
            // 2 bits per sample, 4 samples per byte, high bits first.
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 2; // 4 samples per byte
                int sampleInByte = pixelIndex & 3; // 0..3
                int bitOffset = 6 - (sampleInByte * 2);
                int rawValue = (source[byteIndex] >> bitOffset) & 0x3; // 0..3
                destination[pixelIndex] = (byte)(rawValue * 85); // Scale 0..3 -> 0..255 (255/3 = 85)
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpsampleScaleGray4(byte* source, byte* destination, int columns)
        {
            // 4 bits per sample, 2 samples per byte, high nibble first.
            for (int pixelIndex = 0; pixelIndex < columns; pixelIndex++)
            {
                int byteIndex = pixelIndex >> 1; // 2 samples per byte
                bool highNibble = (pixelIndex & 1) == 0;
                int value = source[byteIndex];
                int rawValue = highNibble ? (value >> 4) : (value & 0xF); // 0..15
                destination[pixelIndex] = (byte)(rawValue * 17); // Scale 0..15 -> 0..255 (255/15 ≈ 17)
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleGray8(byte* source, byte* destination, int columns)
        {
            System.Buffer.MemoryCopy(source, destination, columns, columns);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void UpsampleScaleGray16(byte* source, byte* destination, int columns)
        {
            // 16 bits per sample, big-endian (high byte first). Take high byte only.
            // Updated to use per-pixel pointer stepping (matching UpsampleScaleRgb8 pattern) instead of block 64-bit reads.
            // This avoids potential over-read at the tail and can be faster per benchmarks.
            for (int pixelIndex = 0, sourceOffset = 0; pixelIndex < columns; pixelIndex++, sourceOffset += 2)
            {
                byte highByte = source[sourceOffset];
                destination[pixelIndex] = highByte; // high byte (downscale from 16 -> 8 bits)
            }
        }
    }
}
