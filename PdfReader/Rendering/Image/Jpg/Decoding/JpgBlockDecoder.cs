using System;
using PdfReader.Rendering.Image.Jpg.Readers;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Decodes JPEG 8x8 DCT blocks for baseline sequential scans (non-progressive).
    /// Handles differential DC prediction and run-length / Huffman AC decoding.
    /// </summary>
    internal sealed class JpgBlockDecoder
    {
        /// <summary>
        /// Decode a single 8x8 DCT block (baseline). Coefficients are written in zig‑zag order.
        /// </summary>
        /// <param name="bitReader">Bit reader positioned at the start of the block entropy data.</param>
        /// <param name="dcDecoder">DC Huffman table decoder (required).</param>
        /// <param name="acDecoder">AC Huffman table decoder (required).</param>
        /// <param name="previousDcValue">Reference to previous DC for differential prediction (updated).</param>
        /// <param name="coefficientsZigZag">Destination array (length &gt;= 64) receiving coefficients in zig‑zag order.</param>
        /// <param name="dcOnly">Outputs true if block contains only a DC coefficient (all AC are zero).</param>
        public static void DecodeBaselineBlock(
            ref JpgBitReader bitReader,
            JpgHuffmanDecoder dcDecoder,
            JpgHuffmanDecoder acDecoder,
            ref int previousDcValue,
            int[] coefficientsZigZag,
            out bool dcOnly)
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

            // Ensure prior contents do not leak (hot path: Array.Clear is optimized).
            Array.Clear(coefficientsZigZag, 0, 64);

            DecodeDcCoefficient(ref bitReader, dcDecoder, ref previousDcValue, coefficientsZigZag);
            DecodeAcCoefficients(ref bitReader, acDecoder, coefficientsZigZag, out dcOnly);
        }

        /// <summary>
        /// Decode differential DC coefficient (baseline).
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
                throw new InvalidOperationException("Huffman decode failed for DC coefficient (invalid category).");
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
        /// Decode AC coefficients using JPEG run-length/Huffman rules (EOB, ZRL, runs of zeros).
        /// Sets dcOnly true only if no non-zero AC value is encountered.
        /// </summary>
        private static void DecodeAcCoefficients(
            ref JpgBitReader bitReader,
            JpgHuffmanDecoder acDecoder,
            int[] coefficientsZigZag,
            out bool dcOnly)
        {
            int coefficientIndex = 1;
            dcOnly = true;

            while (coefficientIndex < 64)
            {
                int runSize = acDecoder.Decode(ref bitReader);
                if (runSize < 0)
                {
                    throw new InvalidOperationException($"Huffman decode failed for AC coefficient at position {coefficientIndex}.");
                }

                if (runSize == 0)
                {
                    // EOB: remaining coefficients stay zero; retain dcOnly as-is.
                    return;
                }

                int run = (runSize >> 4) & 0xF;
                int size = runSize & 0xF;

                if (size == 0 && run == 0xF)
                {
                    // ZRL: skip 16 zeros.
                    coefficientIndex += 16;
                    if (coefficientIndex >= 64)
                    {
                        // Exceeded block; stop.
                        return;
                    }
                    continue;
                }

                coefficientIndex += run; // Advance past run zeros.
                if (coefficientIndex >= 64)
                {
                    // Malformed run beyond block; stop gracefully.
                    return;
                }

                int acValue = size > 0 ? bitReader.ReadSigned(size) : 0;

                if (acValue != 0)
                {
                    dcOnly = false;
                }

                coefficientsZigZag[coefficientIndex] = acValue;
                coefficientIndex++;
            }
        }
    }
}