namespace PdfPixel.Jbig2.Parsing;

/// <summary>
/// Fully parsed header for a JBIG2 text region segment (ITU-T T.88 Section 7.4.3).
/// Combines the flag bits from <see cref="Jbig2TextRegionFlags"/> with the non-flag segment
/// parameters that follow them in the header: refinement AT pixel coordinates and the
/// number of symbol instances.
/// </summary>
internal readonly struct Jbig2TextRegionSegmentInfo
{
    public Jbig2TextRegionSegmentInfo(
        in Jbig2TextRegionFlags flags,
        Jbig2AtPixels? refinementAtPixels,
        int numberOfSymbolInstances,
        int symbolIdCodeLength)
    {
        Flags = flags;
        RefinementAtPixels = refinementAtPixels;
        NumberOfSymbolInstances = numberOfSymbolInstances;
        SymbolIdCodeLength = symbolIdCodeLength;
    }

    /// <summary>
    /// Parsed flag bits from the 2-byte segment flags word.
    /// </summary>
    public Jbig2TextRegionFlags Flags { get; }

    /// <summary>
    /// Refinement adaptive template pixel coordinates read from the segment header, or
    /// <see langword="null"/> when refinement AT pixels are not present.
    /// </summary>
    public Jbig2AtPixels? RefinementAtPixels { get; }

    /// <summary>
    /// Total number of symbol instances to place in the region.
    /// </summary>
    public int NumberOfSymbolInstances { get; }

    /// <summary>
    /// Symbol ID code length in bits, derived from the total available symbol count.
    /// </summary>
    public int SymbolIdCodeLength { get; }
}
