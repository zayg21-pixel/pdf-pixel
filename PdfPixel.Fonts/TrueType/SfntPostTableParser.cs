using System;
using System.Collections.Generic;
using PdfPixel.Fonts.Resources;
using PdfPixel.Fonts.Model;

namespace PdfPixel.Fonts.TrueType;

/// <summary>
/// Provides helpers for parsing the 'post' table in SFNT-based fonts (TrueType/OpenType).
/// </summary>
internal static class SfntPostTableParser
{
    /// <summary>
    /// The standard Macintosh glyph ordering used by 'post' table formats 1.0 and 2.0 for
    /// name indices 0-257, as defined in the TrueType specification. This is distinct from
    /// <see cref="PdfPixel.Fonts.Model.PdfFontEncoding.MacRomanEncoding"/>, which is a PDF text encoding, not a glyph order.
    /// </summary>
    private static readonly PdfFontString[] StandardMacGlyphOrder = FontResourceConverter.FromPdfStringBlob(FontResourceLoader.GetResource("PostGlyphOrder.bin"));

    /// <summary>
    /// Parses the 'post' table (format 1.0) and returns a mapping from glyph names to glyph IDs (GIDs).
    /// The font is assumed to contain exactly the standard Macintosh glyph set, in standard order.
    /// </summary>
    /// <param name="postData">The raw bytes of the 'post' table.</param>
    /// <returns>Dictionary mapping glyph names to GIDs.</returns>
    public static Dictionary<PdfFontString, ushort> GetNameToGidFormat1(byte[] postData)
    {
        if (postData == null || postData.Length < 32)
        {
            throw new ArgumentException("Invalid post table data.", nameof(postData));
        }

        uint formatFixed = SnftExtractHelpers.ReadUInt32(postData, 0);
        float format = formatFixed / 65536.0f;
        if (format != 1.0f)
        {
            throw new ArgumentException("Only post table format 1.0 is supported.", nameof(postData));
        }

        Dictionary<PdfFontString, ushort> nameToGid = new(StandardMacGlyphOrder.Length);
        for (int glyphIndex = 0; glyphIndex < StandardMacGlyphOrder.Length; glyphIndex++)
        {
            PdfFontString glyphName = StandardMacGlyphOrder[glyphIndex];
            nameToGid[glyphName] = (ushort)glyphIndex;
        }

        return nameToGid;
    }

    /// <summary>
    /// Parses the 'post' table (format 2.0) and returns a mapping from glyph names to glyph IDs (GIDs).
    /// </summary>
    /// <param name="postData">The raw bytes of the 'post' table.</param>
    /// <returns>Dictionary mapping glyph names to GIDs.</returns>
    public static Dictionary<PdfFontString, ushort> GetNameToGidFormat2(byte[] postData)
    {
        if (postData == null || postData.Length < 32)
        {
            throw new ArgumentException("Invalid post table data.", nameof(postData));
        }

        uint formatFixed = SnftExtractHelpers.ReadUInt32(postData, 0);
        float format = formatFixed / 65536.0f;
        if (format != 2.0f)
        {
            throw new ArgumentException("Only post table format 2.0 is supported.", nameof(postData));
        }

        int numGlyphs = SnftExtractHelpers.ReadUInt16(postData, 32);
        const int glyphNameIndexOffset = 34;
        Dictionary<PdfFontString, ushort> nameToGid = new(numGlyphs);
        List<int> nameIndices = new(numGlyphs);
        for (int glyphIndex = 0; glyphIndex < numGlyphs; glyphIndex++)
        {
            int nameIndex = SnftExtractHelpers.ReadUInt16(postData, glyphNameIndexOffset + (glyphIndex * 2));
            nameIndices.Add(nameIndex);
        }

        int customNameOffset = glyphNameIndexOffset + (numGlyphs * 2);
        int customNamePtr = customNameOffset;
        for (int glyphIndex = 0; glyphIndex < numGlyphs; glyphIndex++)
        {
            int nameIndex = nameIndices[glyphIndex];
            PdfFontString glyphName;
            if (nameIndex < StandardMacGlyphOrder.Length)
            {
                glyphName = StandardMacGlyphOrder[nameIndex];
            }
            else
            {
                if (customNamePtr >= postData.Length)
                {
                    glyphName = SingleByteEncodings.UndefinedCharacter;
                }
                else
                {
                    int len = postData[customNamePtr];
                    customNamePtr++;
                    if (customNamePtr + len > postData.Length)
                    {
                        glyphName = SingleByteEncodings.UndefinedCharacter;
                    }
                    else
                    {
                        glyphName = new PdfFontString(postData.AsMemory().Slice(customNamePtr, len));
                        customNamePtr += len;
                    }
                }
            }

            nameToGid[glyphName] = (ushort)glyphIndex;
        }

        return nameToGid;
    }
}
