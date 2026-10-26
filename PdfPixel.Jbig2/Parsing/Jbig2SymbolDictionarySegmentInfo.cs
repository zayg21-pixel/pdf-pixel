namespace PdfPixel.Jbig2.Parsing;

/// <summary>
/// Fully parsed header for a JBIG2 symbol dictionary segment (ITU-T T.88 Section 7.4.2).
/// Combines the flag bits from <see cref="Jbig2SymbolDictionaryFlags"/> with the non-flag segment
/// parameters that follow them in the header: AT pixel coordinates, refinement AT pixel
/// coordinates, symbol counts, and the byte offset where coded symbol data begins.
/// </summary>
/// <remarks>
/// Construct via <see cref="Jbig2SegmentParser.ParseSymbolDictionaryInfo"/> rather than directly.
/// </remarks>
internal readonly struct Jbig2SymbolDictionarySegmentInfo
{
    public Jbig2SymbolDictionarySegmentInfo(
        in Jbig2SymbolDictionaryFlags flags,
        Jbig2AtPixels? atPixels,
        Jbig2AtPixels? refinementAtPixels,
        int exportedSymbolCount,
        int newSymbolCount,
        int dataOffset)
    {
        Flags = flags;
        AtPixels = atPixels;
        RefinementAtPixels = refinementAtPixels;
        ExportedSymbolCount = exportedSymbolCount;
        NewSymbolCount = newSymbolCount;
        DataOffset = dataOffset;
    }

    /// <summary>
    /// Parsed flag bits from the 2-byte segment flags word.
    /// </summary>
    public Jbig2SymbolDictionaryFlags Flags { get; }

    /// <summary>
    /// Adaptive template pixel coordinates read from the segment header, or
    /// <see langword="null"/> when Huffman coding is active and AT pixels are absent.
    /// </summary>
    public Jbig2AtPixels? AtPixels { get; }

    /// <summary>
    /// Refinement adaptive template pixel coordinates read from the segment header, or
    /// <see langword="null"/> when refinement AT pixels are not present.
    /// </summary>
    public Jbig2AtPixels? RefinementAtPixels { get; }

    /// <summary>
    /// Number of symbols exported by this segment.
    /// </summary>
    public int ExportedSymbolCount { get; }

    /// <summary>
    /// Number of new symbols defined by this segment.
    /// </summary>
    public int NewSymbolCount { get; }

    /// <summary>
    /// Byte offset from the start of the segment data to the first byte of coded symbol data.
    /// All header fields (flags, AT pixels, counts) precede this offset.
    /// </summary>
    public int DataOffset { get; }
}
