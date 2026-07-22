using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A decoded SFNT container: its version tag and the raw content of every table in its directory.
/// </summary>
public sealed class SfntContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntContainer"/> class.
    /// </summary>
    /// <param name="version">The sfnt version tag.</param>
    /// <param name="tables">Every table found in the font's table directory, in directory order.</param>
    public SfntContainer(uint version, IReadOnlyList<SfntTableRecord> tables)
    {
        Version = version;
        Tables = tables;
    }

    /// <summary>
    /// Gets the sfnt version tag: 0x00010000 (or 'true') for TrueType outlines, 'OTTO' for CFF outlines.
    /// </summary>
    public uint Version { get; }

    /// <summary>
    /// Gets every table found in the font's table directory, in directory order.
    /// </summary>
    public IReadOnlyList<SfntTableRecord> Tables { get; }

    /// <summary>
    /// Finds the table with the given tag, if present.
    /// </summary>
    public SfntTableRecord? FindTable(in SfntTableTag tag)
    {
        for (int tableIndex = 0; tableIndex < Tables.Count; tableIndex++)
        {
            if (Tables[tableIndex].Tag == tag)
            {
                return Tables[tableIndex];
            }
        }

        return null;
    }
}
