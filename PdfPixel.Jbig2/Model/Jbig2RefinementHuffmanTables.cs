using PdfPixel.Jbig2.Decoding;

namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Holds the Huffman tables required for text region refinement decoding
/// (ITU-T T.88 Section 6.4.11 / 7.4.3.1.6).
/// When refinement is active, each symbol instance may be individually refined using
/// arithmetic coding embedded within the Huffman stream.
/// </summary>
internal readonly struct Jbig2RefinementHuffmanTables
{
    public Jbig2RefinementHuffmanTables(
        Jbig2HuffmanTable rdwTable,
        Jbig2HuffmanTable rdhTable,
        Jbig2HuffmanTable rdxTable,
        Jbig2HuffmanTable rdyTable,
        Jbig2HuffmanTable sizeTable,
        int template,
        sbyte[] atX,
        sbyte[] atY)
    {
        RdwTable = rdwTable;
        RdhTable = rdhTable;
        RdxTable = rdxTable;
        RdyTable = rdyTable;
        SizeTable = sizeTable;
        Template = template;
        AtX = atX;
        AtY = atY;
    }

    /// <summary>Table for refinement delta width (SBHUFFRDW).</summary>
    public Jbig2HuffmanTable RdwTable { get; }

    /// <summary>Table for refinement delta height (SBHUFFRDH).</summary>
    public Jbig2HuffmanTable RdhTable { get; }

    /// <summary>Table for refinement delta X (SBHUFFRDX).</summary>
    public Jbig2HuffmanTable RdxTable { get; }

    /// <summary>Table for refinement delta Y (SBHUFFRDY).</summary>
    public Jbig2HuffmanTable RdyTable { get; }

    /// <summary>Table for refinement bitmap size (SBHUFFRSIZE).</summary>
    public Jbig2HuffmanTable SizeTable { get; }

    /// <summary>Refinement template index (0 or 1).</summary>
    public int Template { get; }

    /// <summary>Refinement AT pixel X coordinates (template 0 only).</summary>
    public sbyte[] AtX { get; }

    /// <summary>Refinement AT pixel Y coordinates (template 0 only).</summary>
    public sbyte[] AtY { get; }
}
