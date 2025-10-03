using System;
using PdfReader.Rendering.Image.Jpg.Readers;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Specialized decoder for progressive JPEG coefficient blocks.
    /// Handles DC and AC coefficient decoding with successive approximation refinement.
    /// </summary>
    internal sealed class JpgProgressiveBlockDecoder
    {
        /// <summary>
        /// Decode DC coefficient for progressive JPEG.
        /// </summary>
        public static bool DecodeDcCoefficient(
            ref JpgBitReader bitReader,
            JpgHuffmanDecoder dcDecoder,
            ref int previousDcValue,
            int[] coefficients,
            int coefficientBase,
            bool isFirstPass,
            int successiveApproxLow)
        {
            if (dcDecoder == null)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Progressive: missing DC table");
                return false;
            }

            if (isFirstPass)
            {
                // First pass: decode DC difference and apply successive approximation
                int category = dcDecoder.Decode(ref bitReader);
                if (category < 0)
                {
                    Console.Error.WriteLine("[PdfReader][JPEG] Progressive: DC Huffman decode failed");
                    Console.Error.WriteLine($"[PdfReader][JPEG] Debug: coefficientBase={coefficientBase}, successiveApproxLow={successiveApproxLow}");
                    Console.Error.WriteLine($"[PdfReader][JPEG] Debug: bit reader position={bitReader.Position}");
                    return false;
                }

                int dcDifference = 0;
                if (category > 0)
                {
                    dcDifference = bitReader.ReadSigned(category);
                }

                int dcValue = previousDcValue + dcDifference;
                previousDcValue = dcValue;
                coefficients[coefficientBase + 0] = dcValue << successiveApproxLow;
                return true;
            }
            else
            {
                // Refinement pass: refine DC coefficient
                int bit = (int)bitReader.ReadBits(1);
                if (bit != 0)
                {
                    int sign = coefficients[coefficientBase + 0] >= 0 ? 1 : -1;
                    coefficients[coefficientBase + 0] += sign * (1 << successiveApproxLow);
                }

                return true;
            }
        }

        /// <summary>
        /// Decode AC coefficients for progressive JPEG first pass.
        /// </summary>
        public static bool DecodeAcCoefficientsFirstPass(
            ref JpgBitReader bitReader,
            JpgHuffmanDecoder acDecoder,
            int[] coefficients,
            int coefficientBase,
            int spectralStart,
            int spectralEnd,
            int successiveApproxLow,
            ref int eobRun)
        {
            if (acDecoder == null)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Progressive: missing AC table");
                return false;
            }

            // Check if we're in an EOB run from a previous block
            if (eobRun > 0)
            {
                // Skip this block due to EOB run
                eobRun--;
                return true;
            }

            int k = spectralStart;
            while (k <= spectralEnd)
            {
                int rs = acDecoder.Decode(ref bitReader);
                if (rs < 0)
                {
                    Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: AC Huffman decode failed at coefficient position {k}");
                    Console.Error.WriteLine($"[PdfReader][JPEG] Debug: spectralStart={spectralStart}, spectralEnd={spectralEnd}");
                    Console.Error.WriteLine($"[PdfReader][JPEG] Debug: coefficientBase={coefficientBase}, eobRun={eobRun}");
                    Console.Error.WriteLine($"[PdfReader][JPEG] Debug: bit reader position={bitReader.Position}");
                    return false;
                }

                int r = rs >> 4 & 0xF;
                int s = rs & 0xF;

                if (s == 0)
                {
                    if (r < 15)
                    {
                        // EOB or EOBn (End of Block with run count)
                        int eobCount = 1;
                        if (r > 0)
                        {
                            // Read r additional bits for extended EOB run
                            int additionalBits = (int)bitReader.ReadBits(r);
                            eobCount = (1 << r) + additionalBits;
                        }
                        
                        eobRun = eobCount - 1;  // Set remaining EOB runs (current block counts as 1)
                        break; // End this block
                    }
                    else
                    {
                        // r == 15: ZRL (Zero Run Length) - 16 zero coefficients
                        k += 16;
                        continue;
                    }
                }
                else
                {
                    // Non-zero coefficient: skip r zeros, then place coefficient
                    k += r;
                    if (k > spectralEnd)
                    {
                        Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: coefficient index {k} exceeds spectral end {spectralEnd}");
                        break;
                    }

                    // Decode the coefficient magnitude and sign
                    int coefficient = bitReader.ReadSigned(s);
                    int naturalIndex = JpgZigZag.Table[k];
                    coefficients[coefficientBase + naturalIndex] = coefficient << successiveApproxLow;
                    k++;
                }
            }

            return true;
        }

        /// <summary>
        /// Decode AC coefficients for progressive JPEG refinement pass.
        /// </summary>
        public static bool DecodeAcCoefficientsRefinement(
            ref JpgBitReader bitReader,
            JpgHuffmanDecoder acDecoder,
            int[] coefficients,
            int coefficientBase,
            int spectralStart,
            int spectralEnd,
            int successiveApproxHigh,
            int successiveApproxLow,
            ref int eobRun)
        {
            if (acDecoder == null)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Progressive: missing AC table");
                return false;
            }

            int k = spectralStart;

            // If we have an EOB run from previous block, process it
            if (eobRun > 0)
            {
                for (int kk = spectralStart; kk <= spectralEnd; kk++)
                {
                    int naturalIndex = JpgZigZag.Table[kk];
                    int idx = coefficientBase + naturalIndex;
                    int existing = coefficients[idx];

                    if (existing != 0)
                    {
                        int bit = (int)bitReader.ReadBits(1);
                        if (bit != 0)
                        {
                            int sign = existing >= 0 ? 1 : -1;
                            coefficients[idx] = existing + sign * (1 << successiveApproxLow);
                        }
                    }
                }

                eobRun--;
                return true;
            }

            // Process coefficients until EOB
            while (k <= spectralEnd)
            {
                int rs = acDecoder.Decode(ref bitReader);
                if (rs < 0)
                {
                    Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: AC refinement Huffman decode failed at position {k}, rs={rs}");
                    Console.Error.WriteLine($"[PdfReader][JPEG] Debug: spectralStart={spectralStart}, spectralEnd={spectralEnd}, eobRun={eobRun}");
                    return false;
                }

                int r = rs >> 4 & 0xF;
                int s = rs & 0xF;

                if (s == 0)
                {
                    if (r < 15)
                    {
                        // EOBn: read the additional bits for the run count first
                        int eobCount = 1;
                        if (r > 0)
                        {
                            int additionalBits = (int)bitReader.ReadBits(r);
                            eobCount = (1 << r) + additionalBits;
                        }

                        // Then refine existing non-zeros for the remainder of this block
                        for (int kk = k; kk <= spectralEnd; kk++)
                        {
                            int naturalIndex = JpgZigZag.Table[kk];
                            int idx = coefficientBase + naturalIndex;
                            int existing = coefficients[idx];
                            if (existing != 0)
                            {
                                int bit = (int)bitReader.ReadBits(1);
                                if (bit != 0)
                                {
                                    int sign = existing >= 0 ? 1 : -1;
                                    coefficients[idx] = existing + sign * (1 << successiveApproxLow);
                                }
                            }
                        }

                        eobRun = eobCount - 1;
                        return true; // End this block
                    }
                    else
                    {
                        // ZRL: process 16 positions, refining existing non-zeros among them
                        for (int j = 0; j < 16 && k <= spectralEnd; j++)
                        {
                            int naturalIndex = JpgZigZag.Table[k];
                            int idx = coefficientBase + naturalIndex;
                            int existing = coefficients[idx];

                            if (existing != 0)
                            {
                                int bit = (int)bitReader.ReadBits(1);
                                if (bit != 0)
                                {
                                    int sign = existing >= 0 ? 1 : -1;
                                    coefficients[idx] = existing + sign * (1 << successiveApproxLow);
                                }
                            }
                            k++;
                        }
                        continue;
                    }
                }
                else if (s == 1)
                {
                    // New coefficient with magnitude 1: the sign bit must be read immediately
                    int signBit = (int)bitReader.ReadBits(1);
                    int newCoeff = (signBit != 0 ? 1 : -1) * (1 << successiveApproxLow);

                    while (k <= spectralEnd)
                    {
                        int naturalIndex = JpgZigZag.Table[k];
                        int idx = coefficientBase + naturalIndex;

                        if (coefficients[idx] != 0)
                        {
                            int bit = (int)bitReader.ReadBits(1);
                            if (bit != 0)
                            {
                                int sign = coefficients[idx] >= 0 ? 1 : -1;
                                coefficients[idx] += sign * (1 << successiveApproxLow);
                            }
                            k++;
                        }
                        else
                        {
                            if (r == 0)
                            {
                                coefficients[idx] = newCoeff;
                                k++;
                                break;
                            }
                            else
                            {
                                r--;
                                k++;
                            }
                        }
                    }
                }
                else
                {
                    Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: unexpected coefficient size {s} in refinement pass (should be 0 or 1)");
                    return false;
                }
            }

            return true;
        }
    }
}