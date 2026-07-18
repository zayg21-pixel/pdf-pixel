using PdfPixel.Models;
using System.Collections.Generic;

namespace PdfPixel.Fonts.TrueType;

/// <summary>
/// Holds all parsed cmap subtables and 'post' table glyph names for a TrueType (SFNT) font.
/// </summary>
public class SfntFontTables
{
    /// <summary>
    /// List of all cmap subtable entries found in the font.
    /// </summary>
    public List<SfntCMapEntry> CMapEntries { get; } = [];

    /// <summary>
    /// Maps glyph names to glyph IDs (GIDs), parsed from the font's 'post' table.
    /// </summary>
    public Dictionary<PdfString, ushort> NameToGid { get; } = [];

    /// <summary>
    /// Advance width per glyph ID (GID), as a fraction of the em square, parsed from the font's
    /// 'hmtx' table. Glyph IDs beyond the array's length reuse the last entry, per the 'hmtx' table's
    /// own convention. Null if the font's metrics tables could not be read.
    /// </summary>
    public float[]? GidWidths { get; set; }
}
