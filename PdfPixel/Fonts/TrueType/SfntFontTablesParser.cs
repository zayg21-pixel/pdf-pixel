using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Fonts.TrueType;

/// <summary>
/// Extracts font table information from a SKTypeface for SNFT fonts.
/// </summary>
internal static class SfntFontTablesParser
{
    /// <summary>
    /// Extracts all cmap subtables and 'post' table glyph names from the specified <see cref="SKTypeface"/>.
    /// </summary>
    /// <param name="typeface">The SKTypeface to inspect.</param>
    /// <returns><see cref="SfntFontTables"/> with all parsed cmap entries and post-table glyph names.</returns>
    public static SfntFontTables GetSfntFontTables(SKTypeface typeface)
    {
        SfntFontTables tables = new();

        uint postTag = SnftExtractHelpers.ConvertTagToUInt32("post");
        if (typeface.TryGetTableData(postTag, out byte[] postData) && postData?.Length >= 32)
        {
            uint formatFixed = SnftExtractHelpers.ReadUInt32(postData, 0);
            float format = formatFixed / 65536.0f;

            Dictionary<PdfString, ushort>? nameToGid = format switch
            {
                1.0f => SfntPostTableParser.GetNameToGidFormat1(postData),
                2.0f => SfntPostTableParser.GetNameToGidFormat2(postData),
                _ => null
            };

            if (nameToGid != null)
            {
                foreach (KeyValuePair<PdfString, ushort> kvp in nameToGid)
                {
                    tables.NameToGid[kvp.Key] = kvp.Value;
                }
            }
        }

        uint cmapTag = SnftExtractHelpers.ConvertTagToUInt32("cmap");
        if (typeface.TryGetTableData(cmapTag, out byte[] cmapData) && cmapData?.Length >= 4)
        {
            ushort numTables = SnftExtractHelpers.ReadUInt16(cmapData, 2);

            for (int tableIndex = 0; tableIndex < numTables; tableIndex++)
            {
                int recordOffset = 4 + (tableIndex * 8);
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
                SortedDictionary<int, ushort>? codeToGid = SnftCMapParser.Parse(cmapData, format, (int)subtableOffset);

                tables.CMapEntries.Add(new SfntCMapEntry(format, (int)subtableOffset, encoding, codeToGid));
            }
        }

        return tables;
    }
}
