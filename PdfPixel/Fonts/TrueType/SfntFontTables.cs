using PdfPixel.Models;
using System.Collections.ObjectModel;

namespace PdfPixel.Fonts.TrueType;

/// <summary>
/// Holds extracted font table mappings and related information for a TrueType font.
/// </summary>
public class SfntFontTables
{
    /// <summary>
    /// Information about the font's tables and offsets.
    /// </summary>
    public SfntFontTableInfo? FontTableInfo { get; set; }

    /// <summary>
    /// Maps single-byte codes (0-255) to glyph ID (GIDs).
    /// </summary>
    public ushort[]? SingleByteCodeToGid { get; set; }

    /// <summary>
    /// Maps glyph names (<see cref="PdfString"/>) to glyph ID (GIDs).
    /// </summary>
    public ReadOnlyDictionary<PdfString, ushort>? NameToGid { get; set; }

    /// <summary>
    /// Maps Unicode codepoints (as strings) to glyph IDs (GIDs).
    /// </summary>
    public ReadOnlyDictionary<string, ushort>? UnicodeToGid { get; set; }
}
