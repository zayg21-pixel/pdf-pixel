using PdfReader.Models;
using PdfReader.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Fonts.TrueType;

/// <summary>
/// Holds extracted font table mappings and related information for a TrueType font.
/// </summary>
public class SntfFontTables
{
    /// <summary>
    /// Information about the font's tables and offsets.
    /// </summary>
    public FontTableInfo FontTableInfo { get; set; }

    /// <summary>
    /// Maps single-byte codes (0-255) to glyph IDs (GIDs).
    /// </summary>
    public ushort[] SingleByteCodeToGid { get; set; }

    /// <summary>
    /// Maps glyph names (<see cref="PdfString"/>) to glyph IDs (GIDs).
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
internal class SntfFontTableParser
{
    /// <summary>
    /// Extracts all relevant font table mappings from the specified <see cref="SKTypeface"/>.
    /// </summary>
    /// <param name="typeface">The SkiaSharp typeface to extract mappings from.</param>
    /// <returns>A <see cref="SntfFontTables"/> instance containing all extracted mappings and table info.</returns>
    public static SntfFontTables GetSntfFontTables(SKTypeface typeface)
    {
        var tableInfo = SntfFontTableInfoParser.GetFontTableInfo(typeface);

        var codeToGid = ExtractCodeToGidFormat0(tableInfo);

        if (codeToGid == null)
        {
            codeToGid = ExtractCodeToGidFormat4(tableInfo);
        }

        return new SntfFontTables
        {
            FontTableInfo = tableInfo,
            SingleByteCodeToGid = codeToGid,
            NameToGid = ExtractNameToGid(tableInfo),
            UnicodeToGid = ExtractUnicodeToGid(tableInfo)
        };
    }

    /// <summary>
    /// Extracts a direct mapping from byte code to GID using Format 0 CMap.
    /// Used as the primary mapping for single-byte fonts.
    /// </summary>
    private static ushort[] ExtractCodeToGidFormat0(FontTableInfo info)
    {
        if (info.CmapData != null && info.Format0Offset >= 0)
        {
            return SnftCMapParser.ParseFormat0(info.CmapData, info.Format0Offset);
        }
        return null;
    }

    /// <summary>
    /// Extracts a direct mapping from byte code to GID using Format 4 CMap.
    /// </summary>
    private static ushort[] ExtractCodeToGidFormat4(FontTableInfo info)
    {
        if (info.CmapData != null && info.Format4Offset >= 0)
        {
            var subResult = SnftCMapParser.ParseFormat4(info.CmapData, info.Format4Offset);
            ushort[] result = new ushort[256];
            // map is used for symbol fonts with single-byte codes
            // SNTF format 4 mapping uses reserved area for single-byte codes with offset, simple mapping
            // to byte works exactly as expected.

            foreach (var item in subResult)
            {
                result[(byte)item.Key] = item.Value;
            }

            return result;
        }
        return null;
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
                    nameToGid[kvp.Key] = kvp.Value;
                }
            }
            else if (info.PostDataFormat == 2.0f)
            {
                var postMap = SfntPostTableParser.GetNameToGidFormat2(info.PostData);
                foreach (var kvp in postMap)
                {
                    nameToGid[kvp.Key] = kvp.Value;
                }
            }
        }

        // Merge CMap format 0
        if (info.CmapData != null && info.Format0Offset >= 0)
        {
            PdfString[] encodingNames = SingleByteEncodings.GetEncodingSet(info.Format0Encoding);
            if (encodingNames != null)
            {
                var cmapMap = SnftCMapParser.ParseFormat0(info.CmapData, info.Format0Offset);
                for (int code = 0; code < 256; code++)
                {
                    PdfString glyphName = encodingNames[code];

                    if (!glyphName.IsEmpty)
                    {
                        nameToGid[glyphName] = cmapMap[code];
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

        if (info.CmapData != null && info.Format4Offset >= 0)
        {
            var format4Map = SnftCMapParser.ParseFormat4(info.CmapData, info.Format4Offset);
            foreach (var kvp in format4Map)
            {
                string unicodeString = char.ConvertFromUtf32(kvp.Key);
                unicodeToGid[unicodeString] = kvp.Value;
            }
        }

        // TODO: Add support for format 12 (for large Unicode fonts)

        return unicodeToGid;
    }
}
