namespace PdfPixel.Tiff.Parsing;

/// <summary>
/// One entry of a TIFF image file directory, with the file offset its values start at.
/// </summary>
internal readonly struct TiffEntry
{
    public TiffEntry(TiffTag tag, TiffFieldType type, long count, long valueOffset)
    {
        Tag = tag;
        Type = type;
        Count = count;
        ValueOffset = valueOffset;
    }

    public TiffTag Tag { get; }

    public TiffFieldType Type { get; }

    public long Count { get; }

    public long ValueOffset { get; }
}
