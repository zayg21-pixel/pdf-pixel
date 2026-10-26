using System;
using System.Collections.Generic;

namespace PdfPixel.Jpeg.Parsing;

internal static class JpegHuffmanTableExtensions
{
    public static JpegHuffmanTable WithCounts(this JpegHuffmanTable table, byte[] counts)
    {
        for (int i = 0; i < JpegHuffmanTable.MaxCodeLength; i++)
        {
            table.CodeLengthCounts[i] = counts[i];
        }

        return table;
    }
}
