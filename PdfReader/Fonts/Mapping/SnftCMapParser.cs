using PdfReader.Fonts.Types;
using System.Collections.Generic;

namespace PdfReader.Fonts.Mapping
{
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
            int segCount = ExtractHelpers.ReadUInt16(data, offset + 6) / 2;
            int endCodeOffset = offset + 14;
            int startCodeOffset = endCodeOffset + 2 + segCount * 2;
            int idDeltaOffset = startCodeOffset + segCount * 2;
            int idRangeOffsetOffset = idDeltaOffset + segCount * 2;
            int glyphIdArrayOffset = idRangeOffsetOffset + segCount * 2;

            var unicodeToGid = new Dictionary<int, ushort>();

            for (int segIndex = 0; segIndex < segCount; segIndex++)
            {
                int endCode = ExtractHelpers.ReadUInt16(data, endCodeOffset + segIndex * 2);
                int startCode = ExtractHelpers.ReadUInt16(data, startCodeOffset + segIndex * 2);
                short idDelta = (short)ExtractHelpers.ReadUInt16(data, idDeltaOffset + segIndex * 2);
                int idRangeOffset = ExtractHelpers.ReadUInt16(data, idRangeOffsetOffset + segIndex * 2);

                if (endCode == 0xFFFF)
                {
                    continue;
                }

                for (int code = startCode; code <= endCode; code++)
                {
                    ushort glyphId = 0;
                    if (idRangeOffset == 0)
                    {
                        glyphId = (ushort)((code + idDelta) & 0xFFFF);
                    }
                    else
                    {
                        int rangeOffset = idRangeOffset / 2;
                        int glyphIndex = (code - startCode) + rangeOffset + (segIndex - segCount);
                        int glyphArrayIndex = glyphIdArrayOffset + glyphIndex * 2;
                        if (glyphArrayIndex + 1 < data.Length)
                        {
                            ushort glyphIdFromArray = ExtractHelpers.ReadUInt16(data, glyphArrayIndex);
                            if (glyphIdFromArray != 0)
                            {
                                glyphId = (ushort)((glyphIdFromArray + idDelta) & 0xFFFF);
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
        /// Determines the encoding of a format 0 CMap subtable by inspecting platform and encoding IDs.
        /// </summary>
        /// <param name="cmapData">The raw bytes of the font's CMap table.</param>
        /// <param name="subtableOffset">The offset to the format 0 subtable record.</param>
        /// <returns>The detected PdfFontEncoding value, or PdfFontEncoding.Unknown if not recognized.</returns>
        public static PdfFontEncoding GetFormat0Encoding(byte[] cmapData, int subtableOffset)
        {
            // Each subtable record is 8 bytes: platformID (2), encodingID (2), offset (4)
            // The subtableOffset here should be the offset to the record, not the subtable itself
            if (cmapData == null || cmapData.Length < subtableOffset + 8)
            {
                return PdfFontEncoding.Unknown;
            }

            ushort platformId = ExtractHelpers.ReadUInt16(cmapData, subtableOffset);
            ushort encodingId = ExtractHelpers.ReadUInt16(cmapData, subtableOffset + 2);

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
            // IdentityH/IdentityV and CJK encodings are not represented in format 0

            return PdfFontEncoding.Unknown;
        }
    }
}
