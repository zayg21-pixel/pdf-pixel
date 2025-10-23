using System;
using System.Collections.Generic;
using System.Text;
using PdfReader.Fonts.Types;
using PdfReader.Text;

namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// Provides helpers for parsing the 'post' table in SFNT-based fonts (TrueType/OpenType).
    /// </summary>
    internal static class SfntPostTableParser
    {
        /// <summary>
        /// Parses the 'post' table (format 1.0) and returns a mapping from glyph names to glyph IDs (GIDs).
        /// Only standard MacRoman glyph names are used; no custom names.
        /// </summary>
        /// <param name="postData">The raw bytes of the 'post' table.</param>
        /// <returns>Dictionary mapping glyph names to GIDs.</returns>
        public static Dictionary<string, ushort> GetNameToGidFormat1(byte[] postData)
        {
            if (postData == null || postData.Length < 32)
            {
                throw new ArgumentException("Invalid post table data.", nameof(postData));
            }

            uint formatFixed = ExtractHelpers.ReadUInt32(postData, 0);
            float format = formatFixed / 65536.0f;
            if (format != 1.0f)
            {
                throw new ArgumentException("Only post table format 1.0 is supported.", nameof(postData));
            }

            string[] macGlyphNames = SingleByteEncodings.GetEncodingSet(PdfFontEncoding.MacRomanEncoding);
            var nameToGid = new Dictionary<string, ushort>(macGlyphNames.Length, StringComparer.Ordinal);
            for (int glyphIndex = 0; glyphIndex < macGlyphNames.Length; glyphIndex++)
            {
                string glyphName = macGlyphNames[glyphIndex];
                nameToGid[glyphName] = (ushort)glyphIndex;
            }
            return nameToGid;
        }

        /// <summary>
        /// Parses the 'post' table (format 2.0) and returns a mapping from glyph names to glyph IDs (GIDs).
        /// </summary>
        /// <param name="postData">The raw bytes of the 'post' table.</param>
        /// <returns>Dictionary mapping glyph names to GIDs.</returns>
        public static Dictionary<string, ushort> GetNameToGidFormat2(byte[] postData)
        {
            if (postData == null || postData.Length < 32)
            {
                throw new ArgumentException("Invalid post table data.", nameof(postData));
            }

            uint formatFixed = ExtractHelpers.ReadUInt32(postData, 0);
            float format = formatFixed / 65536.0f;
            if (format != 2.0f)
            {
                throw new ArgumentException("Only post table format 2.0 is supported.", nameof(postData));
            }

            int numGlyphs = ExtractHelpers.ReadUInt16(postData, 32);
            int glyphNameIndexOffset = 34;
            Dictionary<string, ushort> nameToGid = new Dictionary<string, ushort>(numGlyphs, StringComparer.Ordinal);
            List<int> nameIndices = new List<int>(numGlyphs);
            for (int glyphIndex = 0; glyphIndex < numGlyphs; glyphIndex++)
            {
                int nameIndex = ExtractHelpers.ReadUInt16(postData, glyphNameIndexOffset + glyphIndex * 2);
                nameIndices.Add(nameIndex);
            }

            string[] macGlyphNames = SingleByteEncodings.GetEncodingSet(PdfFontEncoding.MacRomanEncoding);

            int customNameOffset = glyphNameIndexOffset + numGlyphs * 2;
            int customNamePtr = customNameOffset;
            for (int glyphIndex = 0; glyphIndex < numGlyphs; glyphIndex++)
            {
                int nameIndex = nameIndices[glyphIndex];
                string glyphName;
                if (nameIndex < macGlyphNames.Length)
                {
                    glyphName = macGlyphNames[nameIndex];
                }
                else
                {
                    int customIndex = nameIndex - macGlyphNames.Length;
                    if (customNamePtr >= postData.Length)
                    {
                        glyphName = ".notdef";
                    }
                    else
                    {
                        int len = postData[customNamePtr];
                        customNamePtr++;
                        if (customNamePtr + len > postData.Length)
                        {
                            glyphName = ".notdef";
                        }
                        else
                        {
                            glyphName = Encoding.ASCII.GetString(postData, customNamePtr, len);
                            customNamePtr += len;
                        }
                    }
                }
                nameToGid[glyphName] = (ushort)glyphIndex;
            }
            return nameToGid;
        }
    }
}
