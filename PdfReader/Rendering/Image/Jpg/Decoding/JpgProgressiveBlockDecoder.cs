using System;
using PdfReader.Rendering.Image.Jpg.Readers;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Decoder for progressive JPEG coefficient blocks (DC and AC, first pass and refinement passes).
    /// Coefficients are stored in natural (row-major) order. Spectral indices (zig-zag order) are remapped
    /// to natural indices during decode to avoid later de-zig-zag passes.
    /// </summary>
    internal sealed class JpgProgressiveBlockDecoder
    {
        /// <summary>
        /// Decode DC coefficient (first pass or refinement) for a progressive JPEG block.
        /// DC coefficient (zig index 0) maps to natural index 0.
        /// </summary>
        public static void DecodeDcCoefficient(
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
                throw new ArgumentNullException(nameof(dcDecoder));
            }
            if (coefficients == null)
            {
                throw new ArgumentNullException(nameof(coefficients));
            }
            if (coefficientBase < 0 || coefficientBase + 63 >= coefficients.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(coefficientBase), "Coefficient base outside buffer range.");
            }

            if (isFirstPass)
            {
                int category = dcDecoder.Decode(ref bitReader);
                if (category < 0)
                {
                    throw new InvalidOperationException("Progressive DC Huffman decode failed (invalid category).");
                }

                int dcDifference = 0;
                if (category > 0)
                {
                    dcDifference = bitReader.ReadSigned(category);
                }

                int dcValue = previousDcValue + dcDifference;
                previousDcValue = dcValue;
                coefficients[coefficientBase + 0] = dcValue << successiveApproxLow; // natural index 0
            }
            else
            {
                int bit = (int)bitReader.ReadBits(1);
                if (bit != 0)
                {
                    int sign = coefficients[coefficientBase + 0] >= 0 ? 1 : -1;
                    coefficients[coefficientBase + 0] += sign * (1 << successiveApproxLow);
                }
            }
        }

        /// <summary>
        /// Decode AC coefficients for a progressive JPEG first pass scan.
        /// Spectral indices (zig-zag order) are remapped to natural indices on write.
        /// </summary>
        public static void DecodeAcCoefficientsFirstPass(
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
                throw new ArgumentNullException(nameof(acDecoder));
            }
            if (coefficients == null)
            {
                throw new ArgumentNullException(nameof(coefficients));
            }
            if (coefficientBase < 0 || coefficientBase + 63 >= coefficients.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(coefficientBase), "Coefficient base outside buffer range.");
            }
            if (spectralStart < 1 || spectralStart > 63 || spectralEnd < spectralStart || spectralEnd > 63)
            {
                throw new ArgumentOutOfRangeException(nameof(spectralStart), "Invalid spectral band range.");
            }

            if (eobRun > 0)
            {
                eobRun--;
                return;
            }

            int k = spectralStart;
            while (k <= spectralEnd)
            {
                int rs = acDecoder.Decode(ref bitReader);
                if (rs < 0)
                {
                    throw new InvalidOperationException($"Progressive AC Huffman decode failed at spectral position {k}.");
                }

                int run = (rs >> 4) & 0xF;
                int size = rs & 0xF;

                if (size == 0)
                {
                    if (run < 15)
                    {
                        int eobCount = 1;
                        if (run > 0)
                        {
                            int additionalBits = (int)bitReader.ReadBits(run);
                            eobCount = (1 << run) + additionalBits;
                        }
                        eobRun = eobCount - 1;
                        break;
                    }
                    else
                    {
                        k += 16;
                        continue;
                    }
                }
                else
                {
                    k += run;
                    if (k > spectralEnd)
                    {
                        break; // Malformed stream extension beyond band.
                    }

                    int coefficient = bitReader.ReadSigned(size);
                    int naturalIndex = JpgZigZag.Table[k];
                    coefficients[coefficientBase + naturalIndex] = coefficient << successiveApproxLow;
                    k++;
                }
            }
        }

        /// <summary>
        /// Decode AC coefficients for a progressive JPEG refinement pass (successive approximation > 0).
        /// Operates on coefficients stored in natural order (reads/writes remap spectral indices each access).
        /// </summary>
        public static void DecodeAcCoefficientsRefinement(
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
                throw new ArgumentNullException(nameof(acDecoder));
            }
            if (coefficients == null)
            {
                throw new ArgumentNullException(nameof(coefficients));
            }
            if (coefficientBase < 0 || coefficientBase + 63 >= coefficients.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(coefficientBase), "Coefficient base outside buffer range.");
            }
            if (spectralStart < 1 || spectralStart > 63 || spectralEnd < spectralStart || spectralEnd > 63)
            {
                throw new ArgumentOutOfRangeException(nameof(spectralStart), "Invalid spectral band range.");
            }

            if (eobRun > 0)
            {
                for (int bandIndex = spectralStart; bandIndex <= spectralEnd; bandIndex++)
                {
                    int naturalIndex = JpgZigZag.Table[bandIndex];
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
                return;
            }

            int k = spectralStart;
            while (k <= spectralEnd)
            {
                int rs = acDecoder.Decode(ref bitReader);
                if (rs < 0)
                {
                    throw new InvalidOperationException($"Progressive AC refinement Huffman decode failed at spectral position {k}.");
                }

                int run = (rs >> 4) & 0xF;
                int size = rs & 0xF;

                if (size == 0)
                {
                    if (run < 15)
                    {
                        int eobCount = 1;
                        if (run > 0)
                        {
                            int additionalBits = (int)bitReader.ReadBits(run);
                            eobCount = (1 << run) + additionalBits;
                        }

                        for (int refineIndex = k; refineIndex <= spectralEnd; refineIndex++)
                        {
                            int naturalIndex = JpgZigZag.Table[refineIndex];
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
                        return;
                    }
                    else
                    {
                        for (int zeroRunIndex = 0; zeroRunIndex < 16 && k <= spectralEnd; zeroRunIndex++)
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
                else if (size == 1)
                {
                    int signBit = (int)bitReader.ReadBits(1);
                    int newCoefficient = (signBit != 0 ? 1 : -1) * (1 << successiveApproxLow);

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
                            if (run == 0)
                            {
                                coefficients[idx] = newCoefficient;
                                k++;
                                break;
                            }
                            else
                            {
                                run--;
                                k++;
                            }
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected progressive refinement symbol size {size} (expected 0 or 1).");
                }
            }
        }
    }
}