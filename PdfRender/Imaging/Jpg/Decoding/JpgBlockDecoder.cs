using System;
using System.Runtime.CompilerServices;
using PdfRender.Imaging.Jpg.Huffman;
using PdfRender.Imaging.Jpg.Model;
using PdfRender.Imaging.Jpg.Readers;

namespace PdfRender.Imaging.Jpg.Decoding;

/// <summary>
/// Decodes JPEG 8x8 DCT blocks for baseline sequential scans (non-progressive).
/// Handles differential DC prediction and run-length / Huffman AC decoding.
/// Outputs coefficients directly in natural (row-major) order to avoid a later de-zig-zag pass.
/// </summary>
internal sealed class JpgBlockDecoder
{
    /// <summary>
    /// Decode a single 8x8 DCT block (baseline). Coefficients are written in natural (row-major) order.
    /// </summary>
    /// <param name="bitReader">Bit reader positioned at the start of the block entropy data.</param>
    /// <param name="dcDecoder">DC Huffman table decoder (required).</param>
    /// <param name="acDecoder">AC Huffman table decoder (required).</param>
    /// <param name="previousDcValue">Reference to previous DC for differential prediction (updated).</param>
    /// <param name="coefficientsNatural">Destination array (length &gt;= 64) receiving coefficients in natural order.</param>
    /// <param name="dcOnly">Outputs true if block contains only a DC coefficient (all AC are zero).</param>
    public static void DecodeBaselineBlock(
        ref JpgBitReader bitReader,
        JpgHuffmanDecoder dcDecoder,
        JpgHuffmanDecoder acDecoder,
        ref int previousDcValue,
        ref Block8x8F coefficientsNatural,
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

        coefficientsNatural.Clear();

        DecodeDcCoefficient(ref bitReader, dcDecoder, ref previousDcValue, ref coefficientsNatural);
        DecodeAcCoefficients(ref bitReader, acDecoder, ref coefficientsNatural, out dcOnly);
    }

    /// <summary>
    /// Decode differential DC coefficient (baseline) and store at natural index 0.
    /// </summary>
    private static void DecodeDcCoefficient(
        ref JpgBitReader bitReader,
        JpgHuffmanDecoder dcDecoder,
        ref int previousDcValue,
        ref Block8x8F coefficientsNatural)
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

        ref float destinationRef = ref Unsafe.As<Block8x8F, float>(ref coefficientsNatural);
        destinationRef = dcValue;
    }

    /// <summary>
    /// Decode AC coefficients using JPEG run-length/Huffman rules (EOB, ZRL, runs of zeros).
    /// Stores each decoded non-zero at its natural index (mapping from current spectral/zig-zag position).
    /// Sets dcOnly true only if no non-zero AC value is encountered.
    /// </summary>
    private static void DecodeAcCoefficients(
        ref JpgBitReader bitReader,
        JpgHuffmanDecoder acDecoder,
        ref Block8x8F coefficientsNatural,
        out bool dcOnly)
    {
        int spectralIndex = 1;
        dcOnly = true;

        // Local references to improve JIT optimizations and avoid repeated static field bounds checks.
        byte[] zigZagToNatural = JpgZigZag.Table;
        ref float destinationRef = ref Unsafe.As<Block8x8F, float>(ref coefficientsNatural);

        while (spectralIndex < 64)
        {
            int runSize = acDecoder.Decode(ref bitReader);
            if (runSize < 0)
            {
                throw new InvalidOperationException($"Huffman decode failed for AC coefficient at spectral position {spectralIndex}.");
            }

            if (runSize == 0)
            {
                // EOB: Remaining coefficients are zero.
                return;
            }

            int run = runSize >> 4 & 0xF;
            int size = runSize & 0xF;

            if (size == 0 && run == 0xF)
            {
                // ZRL: Skip 16 zeros in spectral order.
                spectralIndex += 16;
                if (spectralIndex >= 64)
                {
                    return;
                }

                continue;
            }

            spectralIndex += run; // Advance past run zeros.
            if (spectralIndex >= 64)
            {
                // Malformed stream: run extends beyond block.
                return;
            }

            int acValue = bitReader.ReadSigned(size);
            if (acValue != 0)
            {
                dcOnly = false;
            }

            int naturalIndex = zigZagToNatural[spectralIndex];

            Unsafe.Add(ref destinationRef, naturalIndex) = acValue;
            spectralIndex++;
        }
    }
}