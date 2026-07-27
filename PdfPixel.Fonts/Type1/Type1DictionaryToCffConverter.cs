using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using PdfPixel.PostScript.Tokens;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Type1;

/// <summary>
/// Converts a Type1 font represented as a PostScript dictionary into a structured CFF typeface.
/// </summary>
internal static class Type1DictionaryToCffConverter
{
    private const int FirstCustomSid = 391; // CFF spec: custom strings start at SID391.
    private const string FallbackFontName = "UnnamedFont";

    public static CffTypeface? GenerateCffTypefaceFromDictionary(PostScriptDictionary fontDictionary, ILoggerFactory loggerFactory)
    {
        if (fontDictionary == null)
        {
            return null;
        }

        Dictionary<PdfFontString, byte[]> type1CharStrings = Type1FontDictionaryUtilities.GetCharStrings(fontDictionary);
        Dictionary<int, byte[]> type1Subrs = Type1FontDictionaryUtilities.GetSubroutines(fontDictionary);
        float[] fontMatrix = Type1FontDictionaryUtilities.GetFontMatrix(fontDictionary) ?? [0.001f, 0f, 0f, 0.001f, 0f, 0f];
        float[] fontBBox = Type1FontDictionaryUtilities.GetFontBBox(fontDictionary) ?? [0f, 0f, 0f, 0f];

        string fontName = Type1FontDictionaryUtilities.GetFontName(fontDictionary) ?? FallbackFontName;
        PdfFontString[] encodingVector = Type1FontDictionaryUtilities.GetEncodingVector(fontDictionary) ?? Array.Empty<PdfFontString>();

        GlyphCollections glyphCollections = BuildGlyphCollections(type1CharStrings.Keys);

        PdfFontMatrix matrix = PdfFontMatrix.FromArray(fontMatrix) ?? PdfFontMatrix.Default;
        Type1CharStringEvaluator evaluator = new(loggerFactory.CreateLogger<Type1CharStringEvaluator>());

        var characters = new CffCharacter[glyphCollections.OrderedNames.Count];
        for (int gid = 0; gid < characters.Length; gid++)
        {
            PdfFontString glyphName = glyphCollections.OrderedNames[gid];
            if (type1CharStrings.TryGetValue(glyphName, out byte[]? sourceCharString))
            {
                characters[gid] = evaluator.Evaluate(sourceCharString, type1Subrs, type1CharStrings, matrix);
            }
            else
            {
                characters[gid] = new CffCharacter(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, width: 0f, dict: null);
            }
        }

        CffTopDict topDict = new()
        {
            FontMatrix = matrix,
            FontBBox = fontBBox
        };

        CffFont font = new()
        {
            Name = (PdfFontString)fontName,
            Dict = new CffFontDict { TopDict = topDict },
            Charset = new CffCharset { Format = 0, SidsByGid = glyphCollections.Sids },
            Encoding = BuildEncoding(encodingVector, glyphCollections.NameToGid, characters.Length),
            CodeToName = BuildCodeToName(encodingVector),
            NameToGid = glyphCollections.NameToGid,
            Characters = characters
        };

        return new CffTypeface
        {
            MajorVersion = 1,
            MinorVersion = 0,
            Fonts = new CffFont[] { font },
            Strings = [.. glyphCollections.CustomStrings],
            GlobalSubrs = Array.Empty<ReadOnlyMemory<byte>>()
        };
    }

    private static GlyphCollections BuildGlyphCollections(IEnumerable<PdfFontString> glyphNames)
    {
        PdfFontString notdefName = SingleByteEncodings.UndefinedCharacter;

        List<PdfFontString> orderedNames = [notdefName];
        List<ushort> sids = [0];
        List<byte[]> customStrings = [];
        Dictionary<PdfFontString, ushort> nameToGid = new() { [notdefName] = 0 };
        ushort nextSid = FirstCustomSid;

        ushort gid = 1;
        foreach (PdfFontString name in glyphNames)
        {
            if (name == notdefName)
            {
                continue;
            }

            orderedNames.Add(name);

            if (name.IsEmpty)
            {
                sids.Add(0);
            }
            else
            {
                sids.Add(nextSid);
                customStrings.Add(name.Value.ToArray());
                nextSid++;
            }

            nameToGid[name] = gid;
            gid++;
        }

        return new GlyphCollections
        {
            OrderedNames = orderedNames,
            Sids = [.. sids],
            CustomStrings = customStrings,
            NameToGid = nameToGid
        };
    }

    private static PdfFontString[]? BuildCodeToName(PdfFontString[] encodingVector)
    {
        if (encodingVector.Length == 0)
        {
            return null;
        }

        var codeToName = new PdfFontString[256];
        for (int code = 0; code < encodingVector.Length && code < 256; code++)
        {
            codeToName[code] = encodingVector[code];
        }

        return codeToName;
    }

    private static CffEncoding? BuildEncoding(PdfFontString[] encodingVector, Dictionary<PdfFontString, ushort> nameToGid, int glyphCount)
    {
        if (encodingVector.Length == 0)
        {
            return null;
        }

        var gidByCode = new ushort[256];
        var mainTableCodes = new byte[Math.Max(0, glyphCount - 1)];

        for (int code = 0; code < encodingVector.Length && code < 256; code++)
        {
            PdfFontString name = encodingVector[code];
            if (name.IsEmpty || !nameToGid.TryGetValue(name, out ushort gid) || gid == 0)
            {
                continue;
            }

            gidByCode[code] = gid;
            mainTableCodes[gid - 1] = (byte)code;
        }

        return new CffEncoding
        {
            Format = 0,
            MainTableCodes = mainTableCodes,
            GidByCode = gidByCode
        };
    }

    private struct GlyphCollections
    {
        public List<PdfFontString> OrderedNames;
        public ushort[] Sids;
        public List<byte[]> CustomStrings;
        public Dictionary<PdfFontString, ushort> NameToGid;
    }
}
