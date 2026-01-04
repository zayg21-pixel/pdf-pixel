using PdfReader.Models;
using PdfReader.Text;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace PdfReader.Fonts.TrueType;

/// <summary>
/// Holds extracted font table mappings and related information for a TrueType font.
/// </summary>
public class SfntFontTables
{
    /// <summary>
    /// Information about the font's tables and offsets.
    /// </summary>
    public FontTableInfo FontTableInfo { get; set; }

    /// <summary>
    /// Maps single-byte codes (0-255) to glyph ID (GIDs).
    /// </summary>
    public ushort[] SingleByteCodeToGid { get; set; }

    /// <summary>
    /// Maps glyph names (<see cref="PdfString"/>) to glyph ID (GIDs).
    /// </summary>
    public Dictionary<PdfString, ushort> NameToGid { get; set; }

    /// <summary>
    /// Maps Unicode codepoints (as strings) to glyph IDs (GIDs).
    /// </summary>
    public Dictionary<string, ushort> UnicodeToGid { get; set; }
}

/// <summary>
/// Provides methods for extracting font table mappings from a TrueType font using SkiaSharp.
/// </summary>
internal class SfntFontTableParser
{
    /// <summary>
    /// Extracts all relevant font table mappings from the specified <see cref="SKTypeface"/>.
    /// </summary>
    /// <param name="typeface">The SkiaSharp typeface to extract mappings from.</param>
    /// <returns>A <see cref="SfntFontTables"/> instance containing all extracted mappings and table info.</returns>
    public static SfntFontTables GetSfntFontTables(SKTypeface typeface)
    {
        var tableInfo = SfntFontTableInfoParser.GetFontTableInfo(typeface);

        return new SfntFontTables
        {
            FontTableInfo = tableInfo,
            SingleByteCodeToGid = ExtractSingleByteCodeToGid(tableInfo),
            NameToGid = ExtractNameToGid(tableInfo),
            UnicodeToGid = ExtractUnicodeToGid(tableInfo)
        };
    }

    /// <summary>
    /// Builds a single-byte code to GID mapping by combining multiple CMap formats.
    /// Uses format 0 (byte-to-gid array) as the base and merges fallback mappings from formats 4 and 6.
    /// </summary>
    private static ushort[] ExtractSingleByteCodeToGid(FontTableInfo info)
    {
        if (info == null)
        {
            return null;
        }

        if (info.CmapData == null || info.CMapEntries == null || info.CMapEntries.Count == 0)
        {
            return null;
        }

        ushort[] result = null;

        // Base: format 0
        var format0CMap = info.CMapEntries.FirstOrDefault(c => c.Format == 0);
        if (format0CMap != null)
        {
            // ParseFormat0 already returns 256 entries
            result = SnftCMapParser.ParseFormat0(info.CmapData, format0CMap.Offset);
        }

        // Merge fallbacks (formats 4 and 6) in a single pass
        foreach (var cmap in info.CMapEntries)
        {
            if (cmap.Format == 4)
            {
                var format4Map = SnftCMapParser.ParseFormat4(info.CmapData, cmap.Offset);
                result ??= new ushort[256];
                ApplyDictionaryToByteArray(format4Map, result);
            }
            else if (cmap.Format == 6)
            {
                var format6Map = SnftCMapParser.ParseFormat6(info.CmapData, cmap.Offset);
                result ??= new ushort[256];
                ApplyDictionaryToByteArray(format6Map, result);
            }
        }

        return result;
    }

    /// <summary>
    /// Applies parsed CMap dictionary entries to a single-byte mapping array.
    /// Keys outside byte range are truncated to byte, consistent with symbol font behavior.
    /// </summary>
    private static void ApplyDictionaryToByteArray(Dictionary<int, ushort> map, ushort[] target)
    {
        if (map == null || target == null)
        {
            return;
        }

        foreach (var kvp in map)
        {
            int key = kvp.Key;
            ushort value = kvp.Value;
            target[(byte)key] = value;
        }
    }

    /// <summary>
    /// Extracts a mapping from glyph names to glyph IDs (GIDs) using the font's 'post' table and CMap format 0.
    /// Used only as a fallback if direct code-to-GID mapping is unavailable.
    /// </summary>
    /// <param name="info">FontTableInfo struct with table data and offsets.</param>
    /// <returns>Dictionary mapping glyph names to GIDs.</returns>
    private static Dictionary<PdfString, ushort> ExtractNameToGid(FontTableInfo info)
    {
        var nameToGid = new Dictionary<PdfString, ushort>();

        // Merge post table (format 1.0 or 2.0)
        if (info.PostData != null)
        {
            if (info.PostDataFormat == 1.0f)
            {
                var postMap = SfntPostTableParser.GetNameToGidFormat1(info.PostData);
                foreach (var kvp in postMap)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;
                    if (value != 0 && !nameToGid.ContainsKey(key))
                    {
                        nameToGid[key] = value;
                    }
                }
            }
            else if (info.PostDataFormat == 2.0f)
            {
                var postMap = SfntPostTableParser.GetNameToGidFormat2(info.PostData);

                foreach (var kvp in postMap)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;

                    if (value != 0 && !nameToGid.ContainsKey(key))
                    {
                        nameToGid[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        return nameToGid;
    }

    /// <summary>
    /// Extracts a mapping from Unicode codepoints to glyph IDs (GIDs) using CMap format 4.
    /// </summary>
    /// <param name="info">FontTableInfo struct with table data and offsets.</param>
    /// <returns>Dictionary mapping Unicode codepoints to GIDs.</returns>
    private static Dictionary<string, ushort> ExtractUnicodeToGid(FontTableInfo info)
    {
        var unicodeToGid = new Dictionary<string, ushort>();

        foreach (var cmap in info.CMapEntries)
        {
            if (cmap.Format == 4)
            {
                var format4Map = SnftCMapParser.ParseFormat4(info.CmapData, cmap.Offset);

                foreach (var kvp in format4Map)
                {
                    if (!IsValidUnicodeCodepoint(kvp.Key))
                    {
                        continue;
                    }

                    string unicodeString = char.ConvertFromUtf32(kvp.Key);

                    if (!unicodeToGid.ContainsKey(unicodeString))
                    {
                        unicodeToGid[unicodeString] = kvp.Value;
                    }
                }
            }
            else if (cmap.Format == 6)
            {
                var format6Map = SnftCMapParser.ParseFormat6(info.CmapData, cmap.Offset);

                foreach (var kvp in format6Map)
                {
                    if (!IsValidUnicodeCodepoint(kvp.Key))
                    {
                        continue;
                    }

                    string unicodeString = char.ConvertFromUtf32(kvp.Key);

                    if (!unicodeToGid.ContainsKey(unicodeString))
                    {
                        unicodeToGid[unicodeString] = kvp.Value;
                    }
                }
            }
        }

        // TODO: [HIGH] Add support for format 10/12
        return unicodeToGid;
    }

    private static bool IsValidUnicodeCodepoint(int codepoint)
    {
        return codepoint >= 0 && codepoint <= 0x10FFFF && (codepoint < 0xD800 || codepoint > 0xDFFF);
    }
}
