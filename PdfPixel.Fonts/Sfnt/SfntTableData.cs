using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A table's tag together with its final content bytes, ready to be assembled into an SFNT
/// container by <see cref="SfntContainerProcessor.Write"/>. Unlike <see cref="SfntTableRecord"/>,
/// which describes a table's range within a font that was read, this always carries concrete bytes -
/// either freshly serialized from a table model, or resolved from a source stream for a passthrough table.
/// </summary>
public readonly struct SfntTableData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntTableData"/> struct.
    /// </summary>
    /// <param name="tag">The table's 4-byte tag.</param>
    /// <param name="data">The table's content bytes.</param>
    public SfntTableData(in SfntTableTag tag, in ReadOnlyMemory<byte> data)
    {
        Tag = tag;
        Data = data;
    }

    /// <summary>
    /// Gets the table's 4-byte tag.
    /// </summary>
    public SfntTableTag Tag { get; }

    /// <summary>
    /// Gets the table's content bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }
}
