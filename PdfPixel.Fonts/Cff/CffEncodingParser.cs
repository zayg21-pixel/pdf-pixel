using System;
using PdfPixel.Fonts.Model;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Parses custom CFF encoding tables (format 0 and format 1) to produce a code-to-glyph-name mapping.
/// Predefined encodings (Standard, Expert) are handled via SingleByteEncodings instead.
/// </summary>
internal sealed class CffEncodingParser
{
    /// <summary>
    /// Attempts to parse a custom CFF encoding table and build a code-to-name vector.
    /// </summary>
    /// <param name="cffData">The complete CFF data.</param>
    /// <param name="encodingOffset">Offset to the encoding table (must be greater than 1).</param>
    /// <param name="sidByGlyph">GID-to-SID mapping from the charset.</param>
    /// <param name="customStrings">Custom string table from the String INDEX.</param>
    /// <param name="codeToName">Resulting array of 256 glyph names indexed by character code.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public bool TryParseEncoding(
        in ReadOnlySpan<byte> cffData,
        int encodingOffset,
        ushort[] sidByGlyph,
        PdfFontString[] customStrings,
        out PdfFontString[] codeToName)
    {
        codeToName = new PdfFontString[256];

        CffDataReader reader = new(cffData) { Position = encodingOffset };

        if (!reader.TryReadByte(out byte formatByte))
        {
            return false;
        }

        var format = (byte)(formatByte & 0x7F);
        bool hasSupplement = (formatByte & 0x80) != 0;

        bool success = format switch
        {
            0 => TryParseFormat0(ref reader, sidByGlyph, customStrings, codeToName),
            1 => TryParseFormat1(ref reader, sidByGlyph, customStrings, codeToName),
            _ => false
        };

        if (!success)
        {
            return false;
        }

        if (hasSupplement)
        {
            TryParseSupplement(ref reader, customStrings, codeToName);
        }

        return true;
    }

    private static bool TryParseFormat0(
        ref CffDataReader reader,
        ushort[] sidByGlyph,
        PdfFontString[] customStrings,
        PdfFontString[] codeToName)
    {
        if (!reader.TryReadByte(out byte codeCount))
        {
            return false;
        }

        for (int gid = 1; gid <= codeCount; gid++)
        {
            if (!reader.TryReadByte(out byte code))
            {
                return false;
            }

            if (gid < sidByGlyph.Length)
            {
                PdfFontString glyphName = ResolveGlyphName(sidByGlyph[gid], customStrings);
                if (!glyphName.IsEmpty)
                {
                    codeToName[code] = glyphName;
                }
            }
        }

        return true;
    }

    private static bool TryParseFormat1(
        ref CffDataReader reader,
        ushort[] sidByGlyph,
        PdfFontString[] customStrings,
        PdfFontString[] codeToName)
    {
        if (!reader.TryReadByte(out byte rangeCount))
        {
            return false;
        }

        int gid = 1;
        for (int rangeIndex = 0; rangeIndex < rangeCount; rangeIndex++)
        {
            if (!reader.TryReadByte(out byte firstCode))
            {
                return false;
            }

            if (!reader.TryReadByte(out byte leftCount))
            {
                return false;
            }

            for (int codeOffset = 0; codeOffset <= leftCount; codeOffset++)
            {
                int code = firstCode + codeOffset;
                if (code < 256 && gid < sidByGlyph.Length)
                {
                    PdfFontString glyphName = ResolveGlyphName(sidByGlyph[gid], customStrings);
                    if (!glyphName.IsEmpty)
                    {
                        codeToName[code] = glyphName;
                    }
                }

                gid++;
            }
        }

        return true;
    }

    private static void TryParseSupplement(
        ref CffDataReader reader,
        PdfFontString[] customStrings,
        PdfFontString[] codeToName)
    {
        if (!reader.TryReadByte(out byte supplementCount))
        {
            return;
        }

        for (int i = 0; i < supplementCount; i++)
        {
            if (!reader.TryReadByte(out byte code))
            {
                return;
            }

            if (!reader.TryReadUInt16BE(out ushort sid))
            {
                return;
            }

            PdfFontString glyphName = ResolveGlyphName(sid, customStrings);
            if (!glyphName.IsEmpty)
            {
                codeToName[code] = glyphName;
            }
        }
    }

    private static PdfFontString ResolveGlyphName(ushort sid, PdfFontString[] customStrings)
    {
        if (sid < CffData.StandardStrings.Length)
        {
            return CffData.StandardStrings[sid];
        }

        int customIndex = sid - CffData.StandardStrings.Length;
        if ((uint)customIndex < (uint)customStrings.Length)
        {
            return customStrings[customIndex];
        }

        return default;
    }
}
