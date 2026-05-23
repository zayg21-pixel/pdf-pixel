namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// A single line definition used to build a Huffman table.
/// </summary>
public readonly struct Jbig2HuffmanLine
{
    public readonly int RangeLow;
    public readonly int RangeLength;
    public readonly int PrefixLength;
    public readonly bool IsOob;
    public readonly bool IsLowerRange;

    public Jbig2HuffmanLine(int rangeLow, int rangeLength, int prefixLength, bool isOob = false, bool isLowerRange = false)
    {
        RangeLow = rangeLow;
        RangeLength = rangeLength;
        PrefixLength = prefixLength;
        IsOob = isOob;
        IsLowerRange = isLowerRange;
    }
}
