using System;
using System.Collections.Generic;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// A JBIG2 Huffman table consisting of ordered entries.
/// Tables can be standard (predefined in the spec) or user-defined (from Table segments).
/// </summary>
public sealed partial class Jbig2HuffmanTable
{
    /// <summary>
    /// Ordered list of Huffman entries (sorted by prefix length then code).
    /// </summary>
    public List<Jbig2HuffmanEntry> Entries { get; } = [];

    /// <summary>
    /// Builds prefix codes from range definitions (lengths assigned via canonical Huffman algorithm).
    /// Entry order within the same prefix length is preserved (stable sort).
    /// </summary>
    /// <param name="lines">Array of (rangeLow, rangeLength, prefixLength, isOob, isLowerRange) tuples.</param>
    /// <returns>A built Huffman table.</returns>
    public static Jbig2HuffmanTable Build(in ReadOnlySpan<Jbig2HuffmanLine> lines)
    {
        Jbig2HuffmanTable table = new();

        // Filter out entries with PrefixLength=0 (no code assigned per ITU-T T.88 Annex B.3)
        // Sort by prefix length, preserving insertion order for equal lengths (stable sort)
        List<(Jbig2HuffmanLine Line, int Index)> indexed = new(lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].PrefixLength > 0)
            {
                indexed.Add((lines[i], i));
            }
        }

        indexed.Sort((a, b) =>
        {
            int cmp = a.Line.PrefixLength.CompareTo(b.Line.PrefixLength);
            return (cmp != 0) ? cmp : a.Index.CompareTo(b.Index);
        });

        // Assign canonical Huffman codes
        int code = 0;
        int currentLength = 0;

        foreach ((Jbig2HuffmanLine line, int _) in indexed)
        {
            if (line.PrefixLength > currentLength)
            {
                code <<= (line.PrefixLength - currentLength);
                currentLength = line.PrefixLength;
            }

            table.Entries.Add(new Jbig2HuffmanEntry
            {
                PrefixCode = code,
                PrefixLength = line.PrefixLength,
                RangeLength = line.RangeLength,
                RangeLow = line.RangeLow,
                IsOob = line.IsOob,
                IsLowerRange = line.IsLowerRange
            });

            code++;
        }

        return table;
    }
}
