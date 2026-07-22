using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A single SFNT table as extracted from a font's table directory: its tag, its recorded checksum,
/// and the raw bytes of its content.
/// </summary>
public readonly struct SfntTableRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntTableRecord"/> struct.
    /// </summary>
    /// <param name="tag">The table's 4-byte tag.</param>
    /// <param name="checkSum">The checksum recorded for this table in the font's table directory.</param>
    /// <param name="data">The table's raw content bytes.</param>
    public SfntTableRecord(in SfntTableTag tag, uint checkSum, in ReadOnlyMemory<byte> data)
    {
        Tag = tag;
        CheckSum = checkSum;
        Data = data;
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
    /// Gets the table's raw content bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }
}
