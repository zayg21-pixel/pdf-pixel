using PdfReader.Fonts.Model;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Fonts.TrueType;

/// <summary>
/// Represents a single cmap subtable entry in the font.
/// </summary>
public class CMapEntry
{
    public CMapEntry(ushort format, int offset, PdfFontEncoding? encoding)
    {
        Format = format;
        Offset = offset;
        Encoding = encoding;
    }

    /// <summary>
    /// The format number of the cmap subtable (e.g., 0, 4, 6).
    /// </summary>
    public ushort Format { get; }

    /// <summary>
    /// The offset to the subtable in the cmap table data.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// The encoding for this subtable, if detected; otherwise null.
    /// </summary>
    public PdfFontEncoding? Encoding { get; }
}

/// <summary>
/// Holds extracted font table data and offsets for parsing TrueType (SFNT) font tables.
/// </summary>
/// <remarks>
/// This struct is used to store the raw table data and the offsets to specific cmap subtables
/// required for character-to-glyph mapping. It also records the encoding
/// associated with each supported subtable, if detected. Offsets are relative to the start of the
/// cmap table data. If a subtable is not present, its offset is set to -1 and encoding to Unknown.
/// </remarks>
public class FontTableInfo
{
    /// <summary>
    /// List of all cmap subtable entries found in the font.
    /// </summary>
    public List<CMapEntry> CMapEntries { get; } = new List<CMapEntry>();

    /// <summary>
    /// Raw bytes of the 'post' table, if present.
    /// </summary>
    public byte[] PostData { get; set; }

    /// <summary>
    /// The format of the 'post' table as a floating-point value (e.g., 2.0, 3.0).
    /// </summary>
    public float PostDataFormat { get; set; }

    /// <summary>
    /// Raw bytes of the 'cmap' table, if present.
    /// </summary>
    public byte[] CmapData { get; set; }
}

/// <summary>
/// Extracts font table information from a SKTypeface for SNFT fonts.
/// </summary>
internal class SfntFontTableInfoParser
{
    /// <summary>
    /// Extracts font table data and offsets needed for mapping.
    /// </summary>
    /// <param name="typeface">The SKTypeface to inspect.</param>
    /// <returns>FontTableInfo struct with table data and offsets.</returns>
    public static FontTableInfo GetFontTableInfo(SKTypeface typeface)
    {
        FontTableInfo info = new FontTableInfo();

        uint postTag = SnftExtractHelpers.ConvertTagToUInt32("post");
        if (typeface.TryGetTableData(postTag, out byte[] postData) && postData != null && postData.Length >= 32)
        {
            info.PostData = postData;
            uint formatFixed = SnftExtractHelpers.ReadUInt32(postData, 0);
            //info.PostDataFormat = formatFixed / 65536.0f;
        }

        uint cmapTag = SnftExtractHelpers.ConvertTagToUInt32("cmap");
        if (typeface.TryGetTableData(cmapTag, out byte[] cmapData) && cmapData != null && cmapData.Length >= 4)
        {
            info.CmapData = cmapData;
            ushort numTables = SnftExtractHelpers.ReadUInt16(cmapData, 2);

            for (int tableIndex = 0; tableIndex < numTables; tableIndex++)
            {
                int recordOffset = 4 + tableIndex * 8;
                if (recordOffset + 8 > cmapData.Length)
                {
                    continue;
                }
                uint subtableOffset = SnftExtractHelpers.ReadUInt32(cmapData, recordOffset + 4);
                if (subtableOffset + 2 > cmapData.Length)
                {
                    continue;
                }

                ushort format = SnftExtractHelpers.ReadUInt16(cmapData, (int)subtableOffset);
                PdfFontEncoding? encoding = SnftCMapParser.GetFormatEncoding(cmapData, recordOffset);

                // Add all subtables to CMapEntries
                info.CMapEntries.Add(new CMapEntry(format, (int)subtableOffset, encoding));
            }
        }

        return info;
    }
}
