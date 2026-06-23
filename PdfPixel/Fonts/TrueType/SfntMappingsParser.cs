using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.Fonts.TrueType;

/// <summary>
/// Provides methods for extracting font table mappings from a TrueType font using SkiaSharp.
/// </summary>
internal static class SfntFontTableParser
{
    /// <summary>
    /// Extracts all relevant font table mappings from the specified <see cref="SKTypeface"/>.
    /// </summary>
    /// <param name="typeface">The SkiaSharp typeface to extract mappings from.</param>
    /// <returns>A <see cref="SfntFontTables"/> instance containing all extracted mappings and table info.</returns>
    public static SfntFontTables GetSfntFontTables(SKTypeface typeface)
    {
        SfntFontTableInfo tableInfo = SfntFontTableInfoParser.GetFontTableInfo(typeface);

        return new SfntFontTables
        {
            FontTableInfo = tableInfo,
            SingleByteCodeToGid = ExtractSingleByteCodeToGid(tableInfo),
            NameToGid = new System.Collections.ObjectModel.ReadOnlyDictionary<PdfString, ushort>(ExtractNameToGid(tableInfo)),
            UnicodeToGid = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ushort>(ExtractUnicodeToGid(tableInfo))
        };
    }

    /// <summary>
    /// Builds a single-byte code to GID mapping by combining multiple CMap formats.
    /// Uses format 0 (byte-to-gid array) as the base and merges fallback mappings from formats 4 and 6.
    /// </summary>
    private static ushort[]? ExtractSingleByteCodeToGid(SfntFontTableInfo info)
    {
        if (info == null)
        {
            return null;
        }

        if (info.CmapData == null || info.CMapEntries == null || info.CMapEntries.Count == 0)
        {
            return null;
        }

        ushort[]? result = null;

        // as per PDF spec, symbol encoding should be preferred for single-byte fonts, followed by mac roman, then any other encoding as fallback
        ApplyEncodings(ref result, info, info.CMapEntries.Where(x => x.Encoding == Model.PdfFontEncoding.SymbolEncoding));

        if (result != null)
        {
            return result;
        }


        ApplyEncodings(ref result, info, info.CMapEntries.Where(x => x.Encoding == Model.PdfFontEncoding.MacRomanEncoding));

        if (result != null)
        {
            return result;
        }

        ApplyEncodings(ref result, info, info.CMapEntries);

        return result;
    }

    private static void ApplyEncodings(ref ushort[]? result, SfntFontTableInfo info, IEnumerable<SfntCMapEntry> entries)
    {
        if (info.CmapData == null)
        {
            return;
        }

        foreach (SfntCMapEntry entry in entries)
        {
            if (entry.Format == 0)
            {
                result = SnftCMapParser.ParseFormat0(info.CmapData, entry.Offset);
                return;
            }
            else if (entry.Format == 4)
            {
                Dictionary<int, ushort> format4Map = SnftCMapParser.ParseFormat4(info.CmapData, entry.Offset);
                result ??= new ushort[256];
                ApplyDictionaryToByteArray(format4Map, result);
                return;
            }
            else if (entry.Format == 6)
            {
                Dictionary<int, ushort> format6Map = SnftCMapParser.ParseFormat6(info.CmapData, entry.Offset);
                result ??= new ushort[256];
                ApplyDictionaryToByteArray(format6Map, result);
                return;
            }
        }
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

        foreach (KeyValuePair<int, ushort> kvp in map)
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
    private static Dictionary<PdfString, ushort> ExtractNameToGid(SfntFontTableInfo info)
    {
        Dictionary<PdfString, ushort> nameToGid = [];

        // Merge post table (format 1.0 or 2.0)
        if (info.PostData != null)
        {
            if (info.PostDataFormat == 1.0f)
            {
                Dictionary<PdfString, ushort> postMap = SfntPostTableParser.GetNameToGidFormat1(info.PostData);
                foreach (KeyValuePair<PdfString, ushort> kvp in postMap)
                {
                    PdfString key = kvp.Key;
                    ushort value = kvp.Value;
                    if (value != 0 && !nameToGid.ContainsKey(key))
                    {
                        nameToGid[key] = value;
                    }
                }
            }
            else if (info.PostDataFormat == 2.0f)
            {
                Dictionary<PdfString, ushort> postMap = SfntPostTableParser.GetNameToGidFormat2(info.PostData);

                foreach (KeyValuePair<PdfString, ushort> kvp in postMap)
                {
                    PdfString key = kvp.Key;
                    ushort value = kvp.Value;

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
    private static Dictionary<string, ushort> ExtractUnicodeToGid(SfntFontTableInfo info)
    {
        Dictionary<string, ushort> unicodeToGid = [];

        if (info.CmapData == null)
        {
            return unicodeToGid;
        }

        foreach (SfntCMapEntry cmap in info.CMapEntries)
        {
            Dictionary<int, ushort>? codeToGid = null;

            if (cmap.Format == 4)
            {
                codeToGid = SnftCMapParser.ParseFormat4(info.CmapData, cmap.Offset);
            }
            else if (cmap.Format == 6)
            {
                codeToGid = SnftCMapParser.ParseFormat6(info.CmapData, cmap.Offset);
            }

            if (codeToGid == null)
            {
                continue;
            }

            foreach (KeyValuePair<int, ushort> kvp in codeToGid)
            {
                int unicodeCodepoint = ConvertToUnicode(kvp.Key, cmap.Encoding);

                if (!IsValidUnicodeCodepoint(unicodeCodepoint))
                {
                    continue;
                }

                string unicodeString = char.ConvertFromUtf32(unicodeCodepoint);

                if (!unicodeToGid.ContainsKey(unicodeString))
                {
                    unicodeToGid[unicodeString] = kvp.Value;
                }
            }
        }

        // TODO: [HIGH] Add support for format 10/12
        return unicodeToGid;
    }

    private static int ConvertToUnicode(int code, PdfFontEncoding? encoding)
    {
        if (encoding == null
            || encoding == PdfFontEncoding.WinAnsiEncoding
            || encoding == PdfFontEncoding.Unknown)
        {
            return code;
        }

        if (code < 0 || code > 255)
        {
            return code;
        }

        PdfString glyphName = SingleByteEncodings.GetNameByCode((byte)code, encoding.Value);

        if (glyphName.IsEmpty)
        {
            return code;
        }

        if (AdobeGlyphList.CharacterMap.TryGetValue(glyphName, out string? unicode)
            && unicode != null
            && unicode.Length > 0)
        {
            return char.ConvertToUtf32(unicode, 0);
        }

        return code;
    }

    private static bool IsValidUnicodeCodepoint(int codepoint) => codepoint >= 0 && codepoint <= 0x10FFFF && (codepoint < 0xD800 || codepoint > 0xDFFF);
}
