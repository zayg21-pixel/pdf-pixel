using System;
using System.Collections.Generic;
using PdfPixel.Jbig2.Decoding;

namespace PdfPixel.Jbig2.Parsing;

/// <summary>
/// Parses user-defined Huffman tables from JBIG2 Table segments (type 53).
/// User-defined tables supply custom coding tables referenced
/// by symbol dictionary and text region segments.
/// Defined in ITU-T T.88 Section 7.4.13 / Annex B.2.
/// </summary>
internal sealed class Jbig2TableParser
{
    /// <summary>
    /// Parses a user-defined Huffman table from a Tables segment.
    /// </summary>
    /// <param name="segmentData">The raw segment data.</param>
    /// <returns>Parsed Huffman table, or null on failure.</returns>
    public Jbig2HuffmanTable? Parse(in ReadOnlySpan<byte> segmentData)
    {
        if (segmentData.Length < 8)
        {
            return null;
        }

        // Table flags (1 byte)
        byte flags = segmentData[0];
        bool hasOob = (flags & 0x01) != 0;
        int codeSizeFieldLength = ((flags >> 1) & 0x07) + 1; // PS value (1-8)
        int rangeSizeFieldLength = ((flags >> 4) & 0x07) + 1; // RS value (1-8)

        // Low value (4 bytes, signed big-endian at offset 1)
        int lowValue = ReadInt32BigEndian(segmentData, 1);

        // High value (4 bytes, signed big-endian at offset 5)
        int highValue = ReadInt32BigEndian(segmentData, 5);

        int offset = 9 * 8; // bit offset (header is 9 bytes)
        List<Jbig2HuffmanLine> lines = [];

        // Parse table lines
        int bitLength = segmentData.Length * 8;
        int currentLow = lowValue;
        while (currentLow < highValue && offset < bitLength)
        {
            int prefixLength = ReadNBits(segmentData, ref offset, codeSizeFieldLength);
            int rangeLength = ReadNBits(segmentData, ref offset, rangeSizeFieldLength);

            lines.Add(new Jbig2HuffmanLine(currentLow, rangeLength, prefixLength));
            currentLow += 1 << rangeLength;
        }

        // Final upper-range line (covers values >= highValue)
        if (offset < bitLength)
        {
            int finalPrefixLength = ReadNBits(segmentData, ref offset, codeSizeFieldLength);
            lines.Add(new Jbig2HuffmanLine(highValue, 32, finalPrefixLength));
        }

        // Low-value line (covers values < lowValue)
        if (offset < bitLength)
        {
            int lowPrefixLength = ReadNBits(segmentData, ref offset, codeSizeFieldLength);
            lines.Add(new Jbig2HuffmanLine(lowValue - 1, 32, lowPrefixLength, isLowerRange: true));
        }

        // OOB line
        if (hasOob && offset < bitLength)
        {
            int oobPrefixLength = ReadNBits(segmentData, ref offset, codeSizeFieldLength);
            lines.Add(new Jbig2HuffmanLine(0, 0, oobPrefixLength, isOob: true));
        }

        // Build into proper Huffman table
        Jbig2HuffmanLine[] lineArray = lines.ToArray();
        return Jbig2HuffmanTable.Build(lineArray);
    }

    private static int ReadNBits(in ReadOnlySpan<byte> data, ref int bitOffset, int count)
    {
        int result = 0;
        for (int i = 0; i < count; i++)
        {
            int byteIndex = bitOffset / 8;
            int bitIndex = 7 - (bitOffset % 8);

            if (byteIndex < data.Length)
            {
                result = (result << 1) | ((data[byteIndex] >> bitIndex) & 1);
            }

            bitOffset++;
        }

        return result;
    }

    private static int ReadInt32BigEndian(in ReadOnlySpan<byte> data, int offset) => (int)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
}
