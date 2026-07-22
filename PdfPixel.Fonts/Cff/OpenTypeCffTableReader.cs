using System;
using PdfPixel.Fonts.TrueType;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Reads the raw 'CFF ' table out of an OpenType (OTF) font file, when present, without requiring the
/// container itself to be fully well-formed.
/// </summary>
public static class OpenTypeCffTableReader
{
    private const uint TagCff = 0x43464620; // 'CFF '
    private const int SfntHeaderSize = 12;
    private const int TableRecordSize = 16;

    /// <summary>
    /// Attempts to locate and extract the 'CFF ' table from an OpenType font byte stream.
    /// Returns <see langword="false"/> when the data is too short to contain a table directory or has no
    /// 'CFF ' table (e.g. a TrueType/glyf-flavored OpenType font).
    /// </summary>
    /// <param name="openTypeData">The full OpenType font program bytes.</param>
    /// <param name="cffTableData">The extracted 'CFF ' table bytes, when found.</param>
    public static bool TryExtractCffTable(in ReadOnlyMemory<byte> openTypeData, out ReadOnlyMemory<byte> cffTableData)
    {
        cffTableData = ReadOnlyMemory<byte>.Empty;

        byte[] data = openTypeData.ToArray();
        if (data.Length < SfntHeaderSize)
        {
            return false;
        }

        ushort numTables = SnftExtractHelpers.ReadUInt16(data, 4);

        for (int tableIndex = 0; tableIndex < numTables; tableIndex++)
        {
            int recordOffset = SfntHeaderSize + (tableIndex * TableRecordSize);
            if (recordOffset + TableRecordSize > data.Length)
            {
                break;
            }

            uint tag = SnftExtractHelpers.ReadUInt32(data, recordOffset);
            if (tag != TagCff)
            {
                continue;
            }

            uint tableOffset = SnftExtractHelpers.ReadUInt32(data, recordOffset + 8);
            uint tableLength = SnftExtractHelpers.ReadUInt32(data, recordOffset + 12);

            if (tableOffset + tableLength > (uint)data.Length)
            {
                return false;
            }

            cffTableData = openTypeData.Slice((int)tableOffset, (int)tableLength);
            return true;
        }

        return false;
    }
}
