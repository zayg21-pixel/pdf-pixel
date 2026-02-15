using PdfPixel.Models;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Parses CFF charset tables to produce SID-by-GID mappings.
/// </summary>
internal sealed class CffCharsetParser
{
    private static readonly PdfString NotDefGlyphName = (PdfString)".notdef"u8;

    private const int PredefinedCharsetIsoAdobe = 0;
    private const int PredefinedCharsetExpert = 1;
    private const int PredefinedCharsetExpertSubset = 2;

    private const int NotDefSid = 0;
    private const int FirstRealGlyphGid = 1;

    private static readonly Dictionary<PdfString, ushort> StandardNameToSid = BuildStandardNameToSid();

    private static Dictionary<PdfString, ushort> BuildStandardNameToSid()
    {
        var standardNameToSidMap = new Dictionary<PdfString, ushort>(CffData.StandardStrings.Length);
        for (ushort sid = 0; sid < CffData.StandardStrings.Length; sid++)
        {
            var standardName = CffData.StandardStrings[sid];
            if (!standardName.IsEmpty && !standardNameToSidMap.ContainsKey(standardName))
            {
                standardNameToSidMap[standardName] = sid;
            }
        }

        return standardNameToSidMap;
    }

    /// <summary>
    /// Attempts to parse a CFF charset and produce an array of SIDs indexed by GID.
    /// </summary>
    /// <param name="cffData">The complete CFF data.</param>
    /// <param name="charsetOffset">Offset to the charset table (or predefined charset ID).</param>
    /// <param name="glyphCount">Number of glyphs in the font.</param>
    /// <param name="sidByGlyph">Array of SIDs indexed by GID.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public bool TryParseCharset(ReadOnlySpan<byte> cffData, int charsetOffset, int glyphCount, out ushort[] sidByGlyph)
    {
        sidByGlyph = Array.Empty<ushort>();

        if (glyphCount <= 0)
        {
            return false;
        }

        if (charsetOffset <= PredefinedCharsetExpertSubset)
        {
            return TryBuildPredefinedCharsetSids(charsetOffset, glyphCount, out sidByGlyph);
        }

        return TryReadExplicitCharsetSids(cffData, charsetOffset, glyphCount, out sidByGlyph);
    }

    private static bool TryReadExplicitCharsetSids(ReadOnlySpan<byte> cffData, int charsetOffset, int glyphCount, out ushort[] sidByGlyph)
    {
        sidByGlyph = new ushort[glyphCount];
        sidByGlyph[0] = NotDefSid;

        int nextGlyphId = FirstRealGlyphGid;
        var charsetReader = new CffDataReader(cffData)
        {
            Position = charsetOffset
        };

        if (!charsetReader.TryReadByte(out byte format))
        {
            return false;
        }

        switch (format)
        {
            case 0:
                return TryReadFormat0(ref charsetReader, sidByGlyph, glyphCount, ref nextGlyphId);
            case 1:
                return TryReadFormat1(ref charsetReader, sidByGlyph, glyphCount, ref nextGlyphId);
            case 2:
                return TryReadFormat2(ref charsetReader, sidByGlyph, glyphCount, ref nextGlyphId);
            default:
                return false;
        }
    }

    private static bool TryReadFormat0(ref CffDataReader reader, ushort[] sidByGlyph, int glyphCount, ref int nextGlyphId)
    {
        for (; nextGlyphId < glyphCount; nextGlyphId++)
        {
            if (!reader.TryReadUInt16BE(out sidByGlyph[nextGlyphId]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadFormat1(ref CffDataReader reader, ushort[] sidByGlyph, int glyphCount, ref int nextGlyphId)
    {
        while (nextGlyphId < glyphCount)
        {
            if (!reader.TryReadUInt16BE(out ushort rangeFirstSid))
            {
                return false;
            }

            if (!reader.TryReadByte(out byte rangeLeftCount))
            {
                return false;
            }

            int rangeCount = rangeLeftCount + 1;
            for (int i = 0; i < rangeCount && nextGlyphId < glyphCount; i++)
            {
                sidByGlyph[nextGlyphId++] = (ushort)(rangeFirstSid + i);
            }
        }

        return true;
    }

    private static bool TryReadFormat2(ref CffDataReader reader, ushort[] sidByGlyph, int glyphCount, ref int nextGlyphId)
    {
        while (nextGlyphId < glyphCount)
        {
            if (!reader.TryReadUInt16BE(out ushort rangeFirstSid))
            {
                return false;
            }

            if (!reader.TryReadUInt16BE(out ushort rangeLeftCount))
            {
                return false;
            }

            int rangeCount = rangeLeftCount + 1;
            for (int i = 0; i < rangeCount && nextGlyphId < glyphCount; i++)
            {
                sidByGlyph[nextGlyphId++] = (ushort)(rangeFirstSid + i);
            }
        }

        return true;
    }

    private static bool TryBuildPredefinedCharsetSids(int charsetId, int glyphCount, out ushort[] sidByGlyph)
    {
        sidByGlyph = new ushort[glyphCount];
        sidByGlyph[0] = NotDefSid;

        PdfString[] charsetGlyphNames;
        if (!TryGetCharsetNames(charsetId, out charsetGlyphNames))
        {
            return false;
        }

        var seenNames = new HashSet<PdfString>();

        int glyphId = FirstRealGlyphGid;
        for (int i = 0; i < charsetGlyphNames.Length && glyphId < glyphCount; i++)
        {
            var glyphName = charsetGlyphNames[i];
            if (glyphName.IsEmpty || glyphName == NotDefGlyphName)
            {
                continue;
            }

            if (seenNames.Add(glyphName) && TryGetStandardSid(glyphName, out ushort sid))
            {
                sidByGlyph[glyphId++] = sid;
            }
        }

        for (; glyphId < glyphCount; glyphId++)
        {
            sidByGlyph[glyphId] = NotDefSid;
        }

        return true;
    }

    private static bool TryGetCharsetNames(int charsetId, out PdfString[] charsetNames)
    {
        switch (charsetId)
        {
            case PredefinedCharsetIsoAdobe:
                charsetNames = CffData.IsoAdobeStrings;
                return true;
            case PredefinedCharsetExpert:
                charsetNames = CffData.ExpertStrings;
                return true;
            case PredefinedCharsetExpertSubset:
                charsetNames = CffData.ExpertSubsetStrings;
                return true;
            default:
                charsetNames = Array.Empty<PdfString>();
                return false;
        }
    }

    private static bool TryGetStandardSid(PdfString name, out ushort sid)
    {
        if (StandardNameToSid != null && StandardNameToSid.TryGetValue(name, out sid))
        {
            return true;
        }

        sid = NotDefSid;
        return false;
    }
}
