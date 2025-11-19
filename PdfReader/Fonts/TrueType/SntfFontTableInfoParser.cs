using PdfReader.Fonts.Types;
using SkiaSharp;

namespace PdfReader.Fonts.TrueType;

/// <summary>
/// Holds extracted font table data and offsets for parsing.
/// </summary>
public struct FontTableInfo
{
    public byte[] PostData;

    public float PostDataFormat;

    public byte[] CmapData;

    public int Format0Offset;

    public PdfFontEncoding Format0Encoding;

    public int Format4Offset;
}

/// <summary>
/// Extracts font table information from a SKTypeface for SNFT fonts.
/// </summary>
internal class SntfFontTableInfoParser
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
            info.PostDataFormat = formatFixed / 65536.0f;
        }

        uint cmapTag = SnftExtractHelpers.ConvertTagToUInt32("cmap");
        if (typeface.TryGetTableData(cmapTag, out byte[] cmapData) && cmapData != null && cmapData.Length >= 4)
        {
            info.CmapData = cmapData;
            ushort numTables = SnftExtractHelpers.ReadUInt16(cmapData, 2);
            info.Format0Offset = -1;
            info.Format4Offset = -1;
            info.Format0Encoding = PdfFontEncoding.Unknown;

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
                if (format == 0 && info.Format0Offset < 0)
                {
                    info.Format0Offset = (int)subtableOffset;
                    info.Format0Encoding = SnftCMapParser.GetFormat0Encoding(cmapData, recordOffset);
                }
                else if (format == 4 && info.Format4Offset < 0)
                {
                    info.Format4Offset = (int)subtableOffset;
                }
            }
        }

        return info;
    }
}
