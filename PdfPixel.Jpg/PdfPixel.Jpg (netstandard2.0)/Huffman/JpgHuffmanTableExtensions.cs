using System;
using System.Collections.Generic;

namespace PdfPixel.Jpg.Huffman;

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
