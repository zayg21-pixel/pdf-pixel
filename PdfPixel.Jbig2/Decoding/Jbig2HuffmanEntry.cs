namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Represents a single entry in a JBIG2 Huffman table.
/// </summary>
public sealed class Jbig2HuffmanEntry
{
    /// <summary>
    /// Prefix code value.
    /// </summary>
    public int PrefixCode { get; set; }

    /// <summary>
    /// Prefix code length in bits.
    /// </summary>
    public int PrefixLength { get; set; }

    /// <summary>
    /// Number of extra bits to read for the range.
    /// </summary>
    public int RangeLength { get; set; }

    /// <summary>
    /// Low value of the range.
    /// </summary>
    public int RangeLow { get; set; }

    /// <summary>
    /// Whether this entry is the out-of-band (OOB) marker.
    /// </summary>
    public bool IsOob { get; set; }

    /// <summary>
    /// Whether this is a lower-range entry (value = RangeLow - extraBits).
    /// </summary>
    public bool IsLowerRange { get; set; }
}
