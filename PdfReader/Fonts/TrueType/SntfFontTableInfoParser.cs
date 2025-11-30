using PdfReader.Fonts.Model;
using SkiaSharp;

namespace PdfReader.Fonts.TrueType;

/// <summary>
/// Holds extracted font table data and offsets for parsing TrueType (SFNT) font tables.
/// </summary>
/// <remarks>
/// This struct is used to store the raw table data and the offsets to specific cmap subtables
/// required for character-to-glyph mapping. It also records the encoding
/// associated with each supported subtable, if detected. Offsets are relative to the start of the
/// cmap table data. If a subtable is not present, its offset is set to -1 and encoding to Unknown.
/// </remarks>
public struct FontTableInfo
{
    /// <summary>
    /// Raw bytes of the 'post' table, if present.
    /// </summary>
    public byte[] PostData;

    /// <summary>
    /// The format of the 'post' table as a floating-point value (e.g., 2.0, 3.0).
    /// </summary>
    public float PostDataFormat;

    /// <summary>
    /// Raw bytes of the 'cmap' table, if present.
    /// </summary>
    public byte[] CmapData;

    /// <summary>
    /// Offset to Format 0 subtable in the cmap table, or -1 if not present.
    /// </summary>
    public int Format0Offset;

    /// <summary>
    /// Encoding for Format 0 subtable, if detected.
    /// </summary>
    public PdfFontEncoding Format0Encoding;

    /// <summary>
    /// Offset to Format 4 subtable in the cmap table, or -1 if not present.
    /// </summary>
    public int Format4Offset;

    /// <summary>
    /// Offset to Format 6 subtable in the cmap table, or -1 if not present.
    /// </summary>
    public int Format6Offset;

    /// <summary>
    /// Encoding for Format 6 subtable, if detected.
    /// </summary>
    public PdfFontEncoding Format6Encoding;
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
            //info.PostDataFormat = formatFixed / 65536.0f;
        }

        uint cmapTag = SnftExtractHelpers.ConvertTagToUInt32("cmap");
        if (typeface.TryGetTableData(cmapTag, out byte[] cmapData) && cmapData != null && cmapData.Length >= 4)
        {
            info.CmapData = cmapData;
            ushort numTables = SnftExtractHelpers.ReadUInt16(cmapData, 2);
            info.Format0Offset = -1;
            info.Format4Offset = -1;
            info.Format6Offset = -1;
            info.Format0Encoding = PdfFontEncoding.Unknown;
            info.Format6Encoding = PdfFontEncoding.Unknown;

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
                else if (format == 6 && info.Format6Offset < 0)
                {
                    info.Format6Offset = (int)subtableOffset;
                    info.Format6Encoding = SnftCMapParser.GetFormat0Encoding(cmapData, recordOffset);
                }
            }
        }

        return info;
    }
}
