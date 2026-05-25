namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// A single line definition used to build a Huffman table.
/// </summary>
public readonly struct Jbig2HuffmanLine
{
    /// <summary>
    /// Initializes a Huffman table line entry.
    /// </summary>
    /// <param name="rangeLow">Lowest integer value represented by this line.</param>
    /// <param name="rangeLength">Number of additional bits read after the prefix to form the decoded value; 0 for a single value.</param>
    /// <param name="prefixLength">Number of bits in the Huffman prefix code for this line.</param>
    /// <param name="isOob">When true, this line represents the Out-Of-Band symbol.</param>
    /// <param name="isLowerRange">When true, this line represents the open-ended lower range (values below <paramref name="rangeLow"/>).</param>
    public Jbig2HuffmanLine(int rangeLow, int rangeLength, int prefixLength, bool isOob = false, bool isLowerRange = false)
    {
        RangeLow = rangeLow;
        RangeLength = rangeLength;
        PrefixLength = prefixLength;
        IsOob = isOob;
        IsLowerRange = isLowerRange;
    }

    /// <summary>
    /// Lowest integer value covered by this line's range.
    /// </summary>
    public readonly int RangeLow { get; }

    /// <summary>
    /// Number of additional bits appended to the prefix to encode values within the range.</summary>
    public readonly int RangeLength { get; }

    /// <summary>Bit length of the Huffman prefix code assigned to this line.
    /// </summary>
    public readonly int PrefixLength { get; }

    /// <summary>
    /// True if this line represents the Out-Of-Band (OOB) symbol rather than an integer range.
    /// </summary>
    public readonly bool IsOob { get; }

    /// <summary>
    /// True if this line is the open-ended lower range (all values less than <see cref="RangeLow"/>).
    /// </summary>
    public readonly bool IsLowerRange { get; }
}
