using PdfReader.Fonts.Model;
using System.Collections.Generic;

namespace PdfReader.Fonts.TrueType;

/// <summary>
/// Provides helpers for parsing CMap tables in SFNT-based fonts (TrueType/OpenType).
/// </summary>
internal static class SnftCMapParser
{
    /// <summary>
    /// Parses a CMap format 0 subtable and returns an array mapping character codes (0-255) to glyph IDs.
    /// </summary>
    /// <param name="data">The font table data.</param>
    /// <param name="offset">The offset to the format 0 subtable.</param>
    /// <returns>Array of glyph IDs indexed by character code.</returns>
    public static ushort[] ParseFormat0(byte[] data, int offset)
    {
        ushort[] codeToGid = new ushort[256];
        int glyphArrayOffset = offset + 6;
        for (int codepoint = 0; codepoint < 256; codepoint++)
        {
            int glyphOffset = glyphArrayOffset + codepoint;
            if (glyphOffset >= data.Length)
            {
                break;
            }
            codeToGid[codepoint] = data[glyphOffset];
        }
        return codeToGid;
    }

    /// <summary>
    /// Parses a CMap format 4 subtable and returns a mapping from Unicode codepoints to glyph IDs.
    /// </summary>
    /// <param name="data">The font table data.</param>
    /// <param name="offset">The offset to the format 4 subtable.</param>
    /// <returns>Dictionary mapping Unicode codepoints to glyph IDs.</returns>
    public static Dictionary<int, ushort> ParseFormat4(byte[] data, int offset)
    {
        int segCount = SnftExtractHelpers.ReadUInt16(data, offset + 6) / 2;
        int endCodeOffset = offset + 14;
        int startCodeOffset = endCodeOffset + 2 + segCount * 2;
        int idDeltaOffset = startCodeOffset + segCount * 2;
        int idRangeOffsetOffset = idDeltaOffset + segCount * 2;
        int glyphIdArrayOffset = idRangeOffsetOffset + segCount * 2;

        var unicodeToGid = new Dictionary<int, ushort>();

        for (int segIndex = 0; segIndex < segCount; segIndex++)
        {
            int endCode = SnftExtractHelpers.ReadUInt16(data, endCodeOffset + segIndex * 2);
            int startCode = SnftExtractHelpers.ReadUInt16(data, startCodeOffset + segIndex * 2);
            short idDelta = (short)SnftExtractHelpers.ReadUInt16(data, idDeltaOffset + segIndex * 2);
            int idRangeOffset = SnftExtractHelpers.ReadUInt16(data, idRangeOffsetOffset + segIndex * 2);

            if (endCode == 0xFFFF)
            {
                continue;
            }

            for (int code = startCode; code <= endCode; code++)
            {
                ushort glyphId = 0;
                if (idRangeOffset == 0)
                {
                    glyphId = (ushort)(code + idDelta & 0xFFFF);
                }
                else
                {
                    int rangeOffset = idRangeOffset / 2;
                    int glyphIndex = code - startCode + rangeOffset + (segIndex - segCount);
                    int glyphArrayIndex = glyphIdArrayOffset + glyphIndex * 2;
                    if (glyphArrayIndex + 1 < data.Length)
                    {
                        ushort glyphIdFromArray = SnftExtractHelpers.ReadUInt16(data, glyphArrayIndex);
                        if (glyphIdFromArray != 0)
                        {
                            glyphId = (ushort)(glyphIdFromArray + idDelta & 0xFFFF);
                        }
                    }
                }
                if (glyphId != 0)
                {
                    unicodeToGid[code] = glyphId;
                }
            }
        }
        return unicodeToGid;
    }

    /// <summary>
    /// Parses a CMap format 6 subtable and returns a mapping from character codes to glyph IDs.
    /// </summary>
    /// <param name="data">The font table data.</param>
    /// <param name="offset">The offset to the format 6 subtable.</param>
    /// <returns>Dictionary mapping character codes to glyph IDs.</returns>
    public static Dictionary<int, ushort> ParseFormat6(byte[] data, int offset)
    {
        // Format 6 subtable structure:
        // format:      2 bytes (should be 6)
        // length:      2 bytes
        // language:    2 bytes
        // firstCode:   2 bytes
        // entryCount:  2 bytes
        // glyphIdArray: entryCount * 2 bytes
        if (data == null || data.Length < offset + 12)
        {
            return new Dictionary<int, ushort>();
        }

        ushort firstCode = SnftExtractHelpers.ReadUInt16(data, offset + 6);
        ushort entryCount = SnftExtractHelpers.ReadUInt16(data, offset + 8);
        int glyphIdArrayOffset = offset + 10;

        var codeToGid = new Dictionary<int, ushort>();
        for (int i = 0; i < entryCount; i++)
        {
            int glyphOffset = glyphIdArrayOffset + i * 2;
            if (glyphOffset + 1 >= data.Length)
            {
                break;
            }
            ushort glyphId = SnftExtractHelpers.ReadUInt16(data, glyphOffset);
            codeToGid[firstCode + i] = glyphId;
        }
        return codeToGid;
    }

    /// <summary>
    /// Determines the encoding of a CMap subtable by inspecting platform and encoding IDs.
    /// </summary>
    /// <param name="cmapData">The raw bytes of the font's CMap table.</param>
    /// <param name="subtableOffset">The offset to the subtable record.</param>
    /// <returns>The detected PdfFontEncoding value, or PdfFontEncoding.Unknown if not recognized.</returns>
    public static PdfFontEncoding? GetFormatEncoding(byte[] cmapData, int subtableOffset)
    {
        // Each subtable record is 8 bytes: platformID (2), encodingID (2), offset (4)
        // The subtableOffset here should be the offset to the record, not the subtable itself
        if (cmapData == null || cmapData.Length < subtableOffset + 8)
        {
            return default;
        }

        ushort platformId = SnftExtractHelpers.ReadUInt16(cmapData, subtableOffset);
        ushort encodingId = SnftExtractHelpers.ReadUInt16(cmapData, subtableOffset + 2);

        // MacRoman: platform 1, encoding 0
        if (platformId == 1 && encodingId == 0)
        {
            return PdfFontEncoding.MacRomanEncoding;
        }
        // WinAnsi: platform 3, encoding 1
        if (platformId == 3 && encodingId == 1)
        {
            return PdfFontEncoding.WinAnsiEncoding;
        }
        // MacExpert: platform 1, encoding 2
        if (platformId == 1 && encodingId == 2)
        {
            return PdfFontEncoding.MacExpertEncoding;
        }
        // StandardEncoding: platform 1, encoding 1 (rare)
        if (platformId == 1 && encodingId == 1)
        {
            return PdfFontEncoding.StandardEncoding;
        }

        return default;
    }
}
