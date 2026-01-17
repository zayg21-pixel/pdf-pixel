using System;
using System.Collections.Generic;

namespace PdfRender.Imaging.Jpg.Huffman;

/// <summary>
/// Represents a single JPEG Huffman table as declared in a DHT segment.
/// Contains canonical code lengths and the associated values in increasing code order.
/// </summary>
internal sealed class JpgHuffmanTable
{
    public const int MaxCodeLength = 16;

    /// <summary>
    /// Table class: 0 = DC, 1 = AC.
    /// </summary>
    public int TableClass { get; set; }

    /// <summary>
    /// Table identifier (0..3).
    /// </summary>
    public int TableId { get; set; }

    /// <summary>
    /// Code length counts L1..L16 (number of codes for each bit-length 1..16).
    /// </summary>
    public byte[] CodeLengthCounts { get; } = new byte[MaxCodeLength];

    /// <summary>
    /// Huffman values (symbols) in the canonical increasing code order.
    /// </summary>
    public byte[] Values { get; set; }

    /// <summary>
    /// Parse one or more Huffman tables from a DHT segment payload.
    /// </summary>
    public static List<JpgHuffmanTable> ParseDhtPayload(ReadOnlySpan<byte> payload)
    {
        var list = new List<JpgHuffmanTable>();
        int offset = 0;
        while (offset < payload.Length)
        {
            if (offset + 17 > payload.Length)
            {
                throw new ArgumentException("Invalid DHT segment: truncated header");
            }

            byte tcTh = payload[offset];
            offset += 1;

            int tableClass = tcTh >> 4 & 0x0F;
            int tableId = tcTh & 0x0F;

            var counts = new byte[MaxCodeLength];
            int valueCount = 0;
            for (int i = 0; i < MaxCodeLength; i++)
            {
                byte c = payload[offset + i];
                counts[i] = c;
                valueCount += c;
            }

            offset += MaxCodeLength;
            if (offset + valueCount > payload.Length)
            {
                throw new ArgumentException("Invalid DHT segment: values truncated");
            }

            var values = payload.Slice(offset, valueCount).ToArray();
            offset += valueCount;

            list.Add(new JpgHuffmanTable
            {
                TableClass = tableClass,
                TableId = tableId,
                Values = values,
                // copy counts into instance array
            }.WithCounts(counts));
        }

        return list;
    }
}

internal static class JpgHuffmanTableExtensions
{
    public static JpgHuffmanTable WithCounts(this JpgHuffmanTable table, byte[] counts)
    {
        for (int i = 0; i < JpgHuffmanTable.MaxCodeLength; i++)
        {
            table.CodeLengthCounts[i] = counts[i];
        }

        return table;
    }
}
