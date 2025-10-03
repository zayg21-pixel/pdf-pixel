using System;
using PdfReader.Rendering.Image.Jpg.Huffman;
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
        /// Decode a single DCT block (8x8 coefficients) for baseline JPEG.
        /// </summary>
        /// <param name="bitReader">Bit reader positioned at block data</param>
        /// <param name="dcDecoder">DC Huffman decoder</param>
        /// <param name="acDecoder">AC Huffman decoder</param>
        /// <param name="previousDcValue">Previous DC value for differential decoding (updated)</param>
        /// <param name="coefficientsZigZag">Output coefficient array in zig-zag order (64 elements)</param>
        /// <returns>True if decoding succeeded, false on error</returns>
        public static bool DecodeBaselineBlock(
            ref JpgBitReader bitReader,
            JpgHuffmanDecoder dcDecoder,
            JpgHuffmanDecoder acDecoder,
            ref int previousDcValue,
            int[] coefficientsZigZag)
        {
            if (dcDecoder == null || acDecoder == null || coefficientsZigZag == null)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Invalid parameters for block decoding");
                return false;
            }

            if (coefficientsZigZag.Length < 64)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Coefficient array too small (need 64 elements)");
                return false;
            }

            // Clear coefficient array
            for (int i = 0; i < 64; i++)
            {
                coefficientsZigZag[i] = 0;
            }

            // Decode DC coefficient
            if (!DecodeDcCoefficient(ref bitReader, dcDecoder, ref previousDcValue, coefficientsZigZag))
            {
                return false;
            }

            // Decode AC coefficients
            if (!DecodeAcCoefficients(ref bitReader, acDecoder, coefficientsZigZag))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Decode the DC coefficient using differential prediction.
        /// </summary>
        private static bool DecodeDcCoefficient(ref JpgBitReader bitReader, JpgHuffmanDecoder dcDecoder, ref int previousDcValue, int[] coefficientsZigZag)
        {
            int category = dcDecoder.Decode(ref bitReader);
            if (category < 0)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Huffman decode failed for DC coefficient");
                return false;
            }

            int dcDifference = 0;
            if (category > 0)
            {
                dcDifference = bitReader.ReadSigned(category);
            }

            int dcValue = previousDcValue + dcDifference;
            previousDcValue = dcValue;
            coefficientsZigZag[0] = dcValue;

            return true;
        }

        /// <summary>
        /// Decode AC coefficients using run-length encoding.
        /// </summary>
        private static bool DecodeAcCoefficients(ref JpgBitReader bitReader, JpgHuffmanDecoder acDecoder, int[] coefficientsZigZag)
        {
            int coefficientIndex = 1;

            while (coefficientIndex < 64)
            {
                int runSize = acDecoder.Decode(ref bitReader);
                if (runSize < 0)
                {
                    Console.Error.WriteLine($"[PdfReader][JPEG] Huffman decode failed for AC coefficients at position k={coefficientIndex}");
                    return false;
                }

                if (runSize == 0)
                {
                    // End of block (EOB)
                    break;
                }

                int run = runSize >> 4 & 0xF;
                int size = runSize & 0xF;

                if (size == 0 && run == 0xF)
                {
                    // Zero run length 16 (ZRL)
                    coefficientIndex += 16;
                    continue;
                }

                // Skip run of zeros
                coefficientIndex += run;
                if (coefficientIndex >= 64)
                {
                    break;
                }

                // Decode AC coefficient value
                int acValue = bitReader.ReadSigned(size);
                coefficientsZigZag[coefficientIndex] = acValue;
                coefficientIndex++;
            }

            return true;
        }
    }
}