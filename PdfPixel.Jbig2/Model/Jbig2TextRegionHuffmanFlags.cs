namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Parsed flag bits from the JBIG2 text region segment Huffman table selection word
/// (ITU-T T.88 Section 7.4.3.1.6, 2 bytes immediately following the main text region flags
/// when Huffman coding is active).
/// </summary>
internal readonly struct Jbig2TextRegionHuffmanFlags
{
    /// <summary>
    /// Initialises the Huffman flags struct by parsing the raw 2-byte big-endian word.
    /// </summary>
    /// <param name="flagsWord">The 2-byte Huffman flags word.</param>
    public Jbig2TextRegionHuffmanFlags(ushort flagsWord)
    {
        FsSelection = flagsWord & 0x03;
        DsSelection = (flagsWord >> 2) & 0x03;
        DtSelection = (flagsWord >> 4) & 0x03;
        RefinementDwSelection = (flagsWord >> 6) & 0x03;
        RefinementDhSelection = (flagsWord >> 8) & 0x03;
        RefinementDxSelection = (flagsWord >> 10) & 0x03;
        RefinementDySelection = (flagsWord >> 12) & 0x03;
        RefinementSizeSelector = (flagsWord & 0x4000) != 0;
    }

    /// <summary>
    /// Huffman table selection for first S (SBHUFFFS, bits 0–1).
    /// </summary>
    public int FsSelection { get; }

    /// <summary>
    /// Huffman table selection for delta S (SBHUFFDS, bits 2–3).
    /// </summary>
    public int DsSelection { get; }

    /// <summary>
    /// Huffman table selection for delta T (SBHUFFDT, bits 4–5).
    /// </summary>
    public int DtSelection { get; }

    /// <summary>
    /// Huffman table selection for refinement delta width (SBHUFFRDW, bits 6–7).
    /// </summary>
    public int RefinementDwSelection { get; }

    /// <summary>
    /// Huffman table selection for refinement delta height (SBHUFFRDH, bits 8–9).
    /// </summary>
    public int RefinementDhSelection { get; }

    /// <summary>
    /// Huffman table selection for refinement delta X (SBHUFFRDX, bits 10–11).
    /// </summary>
    public int RefinementDxSelection { get; }

    /// <summary>
    /// Huffman table selection for refinement delta Y (SBHUFFRDY, bits 12–13).
    /// </summary>
    public int RefinementDySelection { get; }

    /// <summary>
    /// Refinement bitmap size coding selector (SBHUFFRSIZE, bit 14).
    /// </summary>
    public bool RefinementSizeSelector { get; }
}
