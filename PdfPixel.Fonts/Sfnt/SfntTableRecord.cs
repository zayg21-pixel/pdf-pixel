namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A single SFNT table as extracted from a font's table directory: its tag, its recorded checksum,
/// and the byte range of its content within the font it was read from. Carries no bytes itself -
/// resolve the range via the source <see cref="ReadOnlyFontStream"/>'s <c>GetMemory</c> method.
/// </summary>
public readonly struct SfntTableRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntTableRecord"/> struct.
    /// </summary>
    /// <param name="tag">The table's 4-byte tag.</param>
    /// <param name="checkSum">The checksum recorded for this table in the font's table directory.</param>
    /// <param name="offset">The table's byte offset within the font it was read from.</param>
    /// <param name="length">The table's byte length.</param>
    public SfntTableRecord(in SfntTableTag tag, uint checkSum, int offset, int length)
    {
        Tag = tag;
        CheckSum = checkSum;
        Offset = offset;
        Length = length;
    }

    /// <summary>
    /// Gets the table's 4-byte tag.
    /// </summary>
    public SfntTableTag Tag { get; }

    /// <summary>
    /// Gets the checksum recorded for this table in the font's table directory.
    /// </summary>
    public uint CheckSum { get; }

    /// <summary>
    /// Gets the table's byte offset within the font it was read from.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Gets the table's byte length.
    /// </summary>
    public int Length { get; }
}
