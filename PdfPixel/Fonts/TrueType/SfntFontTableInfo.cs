using System.Collections.Generic;

namespace PdfPixel.Fonts.TrueType;

/// <summary>
/// Holds extracted font table data and offsets for parsing TrueType (SFNT) font tables.
/// </summary>
/// <remarks>
/// This struct is used to store the raw table data and the offsets to specific cmap subtables
/// required for character-to-glyph mapping. It also records the encoding
/// associated with each supported subtable, if detected. Offsets are relative to the start of the
/// cmap table data. If a subtable is not present, its offset is set to -1 and encoding to Unknown.
/// </remarks>
public class SfntFontTableInfo
{
    /// <summary>
    /// List of all cmap subtable entries found in the font.
    /// </summary>
    public List<SfntCMapEntry> CMapEntries { get; } = [];

    /// <summary>
    /// Raw bytes of the 'post' table, if present.
    /// </summary>
    public byte[]? PostData { get; set; }

    /// <summary>
    /// The format of the 'post' table as a floating-point value (e.g., 2.0, 3.0).
    /// </summary>
    public float PostDataFormat { get; set; }

    /// <summary>
    /// Raw bytes of the 'cmap' table, if present.
    /// </summary>
    public byte[]? CmapData { get; set; }
}
