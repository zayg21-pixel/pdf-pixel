using System;

namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Parsed flag bits from a JBIG2 symbol dictionary segment header (ITU-T T.88 Section 7.4.2.1).
/// Accepts the raw 2-byte big-endian flags word and exposes each field as a typed, read-only property.
/// </summary>
/// <remarks>
/// Adaptive template (AT) pixel coordinates and refinement AT pixel coordinates are not flag bits;
/// they are data-dependent byte sequences that follow the flags word in the segment header.
/// Read them via <c>Jbig2Templates.ReadAtPixelPairs</c> and advance the offset by <c>count * 2</c>.
/// </remarks>
internal readonly struct Jbig2SymbolDictionaryFlags
{
    /// <summary>
    /// Initialises the flags struct by parsing the raw 2-byte big-endian flags word.
    /// </summary>
    /// <param name="flagsWord">The 2-byte flags word read from the segment header.</param>
    public Jbig2SymbolDictionaryFlags(ushort flagsWord)
    {
        UseHuffman = (flagsWord & 0x0001) != 0;
        UseRefinementAggregation = (flagsWord & 0x0002) != 0;
        HuffDhSelection = (flagsWord >> 2) & 0x03;
        HuffDwSelection = (flagsWord >> 4) & 0x03;
        HuffBmSizeSelection = (flagsWord >> 6) & 0x01;
        HuffAggInstSelection = (flagsWord >> 7) & 0x01;
        ContextUsed = (flagsWord & 0x0100) != 0;
        ContextRetained = (flagsWord & 0x0200) != 0;
        Template = (flagsWord >> 10) & 0x03;
        RefinementTemplate = (flagsWord >> 12) & 0x01;
    }

    /// <summary>Whether Huffman coding is used (false = arithmetic).</summary>
    public bool UseHuffman { get; }

    /// <summary>Whether refinement/aggregate coding is used.</summary>
    public bool UseRefinementAggregation { get; }

    /// <summary>Huffman DH table selection (bits 2–3).</summary>
    public int HuffDhSelection { get; }

    /// <summary>Huffman DW table selection (bits 4–5).</summary>
    public int HuffDwSelection { get; }

    /// <summary>Huffman bitmap size table selection (bit 6).</summary>
    public int HuffBmSizeSelection { get; }

    /// <summary>Huffman aggregate instances table selection (bit 7).</summary>
    public int HuffAggInstSelection { get; }

    /// <summary>
    /// Whether bitmap coding contexts from a referred-to segment should be used
    /// instead of creating fresh contexts (bit 8, ITU-T T.88 Section 7.4.2.2 step 3).
    /// </summary>
    public bool ContextUsed { get; }

    /// <summary>
    /// Whether bitmap coding contexts should be retained after decoding for use by
    /// subsequent segments (bit 9, ITU-T T.88 Section 7.4.2.2 step 7).
    /// </summary>
    public bool ContextRetained { get; }

    /// <summary>Template for direct-coded symbols (0–3).</summary>
    public int Template { get; }

    /// <summary>Refinement template (0–1).</summary>
    public int RefinementTemplate { get; }

}
