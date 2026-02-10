using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Streams
{
    /// <summary>
    /// Provides helper methods for undoing the TIFF predictor (value += left) as defined by PDF specification
    /// when /Predictor = 2. Handles both byte-aligned (8/16 bpc) and packed sub-byte (1/2/4 bpc) samples.
    /// This class is pure and performs all operations in-place on the provided row buffer.
    /// </summary>
    internal static class TiffPredictorUndo
    {
        /// <summary>
        /// Undo TIFF horizontal differencing predictor (each sample becomes sample + leftSample modulo sample domain).
        /// For byte-aligned samples this is performed directly on the byte stream; for packed samples the row is
        /// temporarily unpacked into an integer array, predictor is applied, then repacked preserving original packing.
        /// </summary>
        /// <param name="row">Row buffer containing encoded (predicted) samples. Modified in place to decoded form.</param>
        /// <param name="columns">Number of pixel columns in the image row.</param>
        /// <param name="colors">Number of color components per pixel (samples per pixel).</param>
        /// <param name="bitsPerComponent">Bits per component (1,2,4,8,16 supported).</param>
        /// <param name="bytesPerSample">Bytes per sample (1 for <=8 bpc, 2 for 16 bpc). For packed sub-byte samples this remains 1.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UndoTiffPredictor(byte[] row, int columns, int colors, int bitsPerComponent, int bytesPerSample)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }
            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }
            if (colors <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(colors));
            }
            if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 && bitsPerComponent != 8 && bitsPerComponent != 16)
            {
                throw new NotSupportedException("Unsupported bitsPerComponent for TIFF predictor undo.");
            }

            int samplesPerRow = columns * colors;

            // Byte-aligned path (8 or 16 bits per component) operates directly on byte array.
            if (bitsPerComponent >= 8)
            {
                if (bytesPerSample == 1)
                {
                    // 8-bit samples: simple modular 256 accumulation.
                    for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                    {
                        int leftIndex = sampleIndex - colors;
                        int left = leftIndex >= 0 ? row[leftIndex] : 0;
                        int current = row[sampleIndex];
                        row[sampleIndex] = (byte)((current + left) & 0xFF);
                    }
                }
                else
                {
                    // 16-bit samples: big-endian per PDF spec.
                    for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                    {
                        int byteIndex = sampleIndex * 2;
                        int current = (row[byteIndex] << 8) | row[byteIndex + 1];
                        int left = 0;
                        if (sampleIndex >= colors)
                        {
                            int leftByteIndex = (sampleIndex - colors) * 2;
                            left = (row[leftByteIndex] << 8) | row[leftByteIndex + 1];
                        }
                        int decoded = (current + left) & 0xFFFF;
                        row[byteIndex] = (byte)(decoded >> 8);
                        row[byteIndex + 1] = (byte)(decoded & 0xFF);
                    }
                }
                return;
            }

            // Packed (sub-byte) path: unpack -> apply predictor -> repack.
            int bits = bitsPerComponent;
            int sampleMask = (1 << bits) - 1;
            int[] samples = new int[samplesPerRow];
            int bitPos = 0;

            // Unpack samples from bit stream into integer array preserving order.
            for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
            {
                int byteIndex = bitPos >> 3; // which byte contains start of this sample bits
                int intraBits = bitPos & 7;   // bit offset inside the byte
                int remainingBits = 8 - intraBits;
                int value;
                if (remainingBits >= bits)
                {
                    int shift = remainingBits - bits; // shift right to align sample
                    value = (row[byteIndex] >> shift) & sampleMask;
                }
                else
                {
                    // Sample spans two bytes.
                    int firstPart = row[byteIndex] & ((1 << remainingBits) - 1);
                    int secondPart = row[byteIndex + 1] >> (8 - (bits - remainingBits));
                    value = ((firstPart << (bits - remainingBits)) | secondPart) & sampleMask;
                }
                int leftSampleIndex = sampleIndex - colors;
                int left = leftSampleIndex >= 0 ? samples[leftSampleIndex] : 0;
                samples[sampleIndex] = (value + left) & sampleMask;
                bitPos += bits;
            }

            // Clear destination row before repacking to avoid stale bits.
            Array.Clear(row, 0, row.Length);

            int outBitPos = 0;
            for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
            {
                int value = samples[sampleIndex] & sampleMask;
                int outByteIndex = outBitPos >> 3;
                int outIntra = outBitPos & 7;
                int freeBits = 8 - outIntra;
                if (freeBits >= bits)
                {
                    int shift = freeBits - bits;
                    int mask = sampleMask << shift;
                    row[outByteIndex] = (byte)((row[outByteIndex] & ~mask) | ((value & sampleMask) << shift));
                }
                else
                {
                    int firstBits = freeBits;
                    int secondBits = bits - firstBits;
                    int firstMask = (1 << firstBits) - 1;
                    int firstValue = (value >> secondBits) & firstMask;
                    row[outByteIndex] = (byte)((row[outByteIndex] & ~firstMask) | firstValue);
                    int secondValue = value & ((1 << secondBits) - 1);
                    int secondShift = 8 - secondBits;
                    int secondMask = ((1 << secondBits) - 1) << secondShift;
                    row[outByteIndex + 1] = (byte)((row[outByteIndex + 1] & ~secondMask) | (secondValue << secondShift));
                }
                outBitPos += bits;
            }
        }
    }
}
