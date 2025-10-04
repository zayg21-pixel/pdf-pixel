using System;
using PdfReader.Rendering.Image.Jpg.Readers;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Decodes JPEG DCT blocks using Huffman decoding for baseline sequential scans.
    /// Handles DC and AC coefficient decoding with differential DC prediction.
    /// </summary>
    internal sealed class JpgBlockDecoder
    {
        /// <summary>
        /// Decode a single 8x8 DCT block for baseline JPEG. Coefficients are produced in zig-zag order.
        /// The provided coefficients array is cleared before decoding.
        /// </summary>
        /// <param name="bitReader">Bit reader positioned at the start of the block's entropy-coded data.</param>
        /// <param name="dcDecoder">DC Huffman decoder (must not be null).</param>
        /// <param name="acDecoder">AC Huffman decoder (must not be null).</param>
        /// <param name="previousDcValue">Previous DC coefficient for differential prediction (updated with new DC).</param>
        /// <param name="coefficientsZigZag">Destination array for the 64 coefficients in zig-zag order.</param>
        public static void DecodeBaselineBlock(
            ref JpgBitReader bitReader,
            JpgHuffmanDecoder dcDecoder,
            JpgHuffmanDecoder acDecoder,
            ref int previousDcValue,
            int[] coefficientsZigZag)
        {
            if (dcDecoder == null)
            {
                throw new ArgumentNullException(nameof(dcDecoder));
            }
            if (acDecoder == null)
            {
                throw new ArgumentNullException(nameof(acDecoder));
            }
            if (coefficientsZigZag == null)
            {
                throw new ArgumentNullException(nameof(coefficientsZigZag));
            }
            if (coefficientsZigZag.Length < 64)
            {
                throw new ArgumentException("Coefficient array must have length >= 64.", nameof(coefficientsZigZag));
            }

            // Clear coefficients (baseline always writes all non-zero positions explicitly after decode)
            for (int index = 0; index < 64; index++)
            {
                coefficientsZigZag[index] = 0;
            }

            DecodeDcCoefficient(ref bitReader, dcDecoder, ref previousDcValue, coefficientsZigZag);
            DecodeAcCoefficients(ref bitReader, acDecoder, coefficientsZigZag);
        }

        /// <summary>
        /// Decode the DC coefficient using differential prediction.
        /// </summary>
        private static void DecodeDcCoefficient(
            ref JpgBitReader bitReader,
            JpgHuffmanDecoder dcDecoder,
            ref int previousDcValue,
            int[] coefficientsZigZag)
        {
            int category = dcDecoder.Decode(ref bitReader);
            if (category < 0)
            {
                throw new InvalidOperationException("Huffman decode failed for DC coefficient (invalid category)." );
            }

            int dcDifference = 0;
            if (category > 0)
            {
                dcDifference = bitReader.ReadSigned(category);
            }

            int dcValue = previousDcValue + dcDifference;
            previousDcValue = dcValue;
            coefficientsZigZag[0] = dcValue;
        }

        /// <summary>
        /// Decode AC coefficients using run-length encoding (EOB, ZRL) rules.
        /// </summary>
        private static void DecodeAcCoefficients(
            ref JpgBitReader bitReader,
            JpgHuffmanDecoder acDecoder,
            int[] coefficientsZigZag)
        {
            int coefficientIndex = 1;
            while (coefficientIndex < 64)
            {
                int runSize = acDecoder.Decode(ref bitReader);
                if (runSize < 0)
                {
                    throw new InvalidOperationException($"Huffman decode failed for AC coefficient at position {coefficientIndex}.");
                }

                if (runSize == 0)
                {
                    // End of block (EOB)
                    break;
                }

                int run = (runSize >> 4) & 0xF;
                int size = runSize & 0xF;

                if (size == 0 && run == 0xF)
                {
                    // Zero run length 16 (ZRL): skip 16 zeros
                    coefficientIndex += 16;
                    continue;
                }

                coefficientIndex += run; // Skip specified zeros
                if (coefficientIndex >= 64)
                {
                    // Malformed stream (run goes beyond block). Spec allows decoder to stop.
                    break;
                }

                int acValue = size > 0 ? bitReader.ReadSigned(size) : 0;
                coefficientsZigZag[coefficientIndex] = acValue;
                coefficientIndex++;
            }
        }
    }
}