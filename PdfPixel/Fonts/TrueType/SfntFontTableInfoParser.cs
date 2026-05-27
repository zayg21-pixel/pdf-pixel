using PdfPixel.Fonts.Model;
using SkiaSharp;

namespace PdfPixel.Fonts.TrueType;

/// <summary>
/// Extracts font table information from a SKTypeface for SNFT fonts.
/// </summary>
internal static class SfntFontTableInfoParser
{
    /// <summary>
    /// Extracts font table data and offsets needed for mapping.
    /// </summary>
    /// <param name="typeface">The SKTypeface to inspect.</param>
    /// <returns>FontTableInfo struct with table data and offsets.</returns>
    public static SfntFontTableInfo GetFontTableInfo(SKTypeface typeface)
    {
        SfntFontTableInfo info = new();

        uint postTag = SnftExtractHelpers.ConvertTagToUInt32("post");
        if (typeface.TryGetTableData(postTag, out byte[] postData) && postData?.Length >= 32)
        {
            info.PostData = postData;
        }

        uint cmapTag = SnftExtractHelpers.ConvertTagToUInt32("cmap");
        if (typeface.TryGetTableData(cmapTag, out byte[] cmapData) && cmapData?.Length >= 4)
        {
            info.CmapData = cmapData;
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

                // Add all subtables to CMapEntries
                info.CMapEntries.Add(new SfntCMapEntry(format, (int)subtableOffset, encoding));
            }
        }

        return info;
    }
}
