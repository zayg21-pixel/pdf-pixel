using PdfReader.Fonts.Cff;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.PostScript.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PdfReader.Fonts.Type1;

/// <summary>
/// Converts a Type1 font represented as a PostScript dictionary into CFF font data.
/// </summary>
internal static class Type1DictionaryToCffConverter
{
    private const int FirstCustomSid = 391; // CFF spec: custom strings start at SID391.

    public static CffInfo GenerateCffFontDataFromDictionary(PostScriptDictionary fontDictionary, PdfFontDescriptor descriptor)
    {
        if (fontDictionary == null)
        {
            return null;
        }
        if (descriptor == null)
        {
            return null;
        }

        Dictionary<PdfString, byte[]> type1CharStrings = Type1FontDictionaryUtilities.GetCharStrings(fontDictionary);
        Dictionary<int, byte[]> type1Subrs = Type1FontDictionaryUtilities.GetSubroutines(fontDictionary); // Source local subrs for flattening.
        float[] fontMatrix = Type1FontDictionaryUtilities.GetFontMatrix(fontDictionary) ?? [0.001f, 0f, 0f, 0.001f, 0f, 0f];
        float[] effectiveFontBBox = Type1FontDictionaryUtilities.GetFontBBox(fontDictionary) ?? [0f, 0f, 0f, 0f];

        string fontName = Type1FontDictionaryUtilities.GetFontName(fontDictionary);
        if (string.IsNullOrEmpty(fontName) && descriptor.FontName.ToString() != null)
        {
            fontName = descriptor.FontName.ToString();
        }
        if (string.IsNullOrEmpty(fontName))
        {
            fontName = "UnnamedFont";
        }

        var parameters = new Type1ConverterContext
        {
            Source = type1CharStrings,
            LocalSubrs = type1Subrs
        };

        Dictionary<PdfString, byte[]> type2CharStrings = Type1CharStringConverter.ConvertAllCharStringsToType2Flatten(parameters);

        PdfString[] encodingVector = Type1FontDictionaryUtilities.GetEncodingVector(fontDictionary) ?? Array.Empty<PdfString>();

        var glyphCollections = BuildGlyphCollections(type2CharStrings);
        List<byte[]> orderedCharStrings = glyphCollections.OrderedCharStrings;
        ushort[] sids = glyphCollections.Sids;
        List<byte[]> customStrings = glyphCollections.CustomStrings;
        Dictionary<PdfString, ushort> nameToGid = glyphCollections.NameToGid;

        byte[] charStringsIndex = CffIndexBuilder.BuildIndex(orderedCharStrings);
        byte[] stringIndex = CffIndexBuilder.BuildIndex(customStrings); // May be empty.
        byte[] charsetData = BuildCharsetFormat0(sids); // Skips GID0 when writing.

        byte[] header = CffIndexBuilder.BuildHeader();
        byte[] nameIndex = CffIndexBuilder.BuildSingleObjectIndex(Encoding.ASCII.GetBytes(fontName));
        byte[] globalSubrsIndex = CffIndexBuilder.BuildEmptyIndex();
        byte[] encodingData = BuildCustomEncoding(encodingVector.Length);

        byte[] topDictIndex = Array.Empty<byte>();
        int topDictIndexSize = 0;
        int iterationCount = 0;
        const int MaxIterations = 5;
        int encodingOffset = 0;
        int charsetOffset = 0;
        int charStringsOffset = 0;
        int privateDictOffset = 0;

        while (true)
        {
            iterationCount++;
            int offset = 0;
            offset += header.Length;
            offset += nameIndex.Length;
            int topDictDataStart = offset;
            int stringIndexOffset = topDictDataStart + topDictIndexSize;
            int globalSubrsOffset = stringIndexOffset + stringIndex.Length;
            encodingOffset = globalSubrsOffset + globalSubrsIndex.Length;
            charsetOffset = encodingOffset + encodingData.Length;
            charStringsOffset = charsetOffset + charsetData.Length;
            privateDictOffset = charStringsOffset + charStringsIndex.Length;
            byte[] topDictData = BuildTopDict(effectiveFontBBox, fontMatrix, encodingOffset, charsetOffset, charStringsOffset, privateSize: 0, privateDictOffset);
            topDictIndex = CffIndexBuilder.BuildSingleObjectIndex(topDictData);
            int newSize = topDictIndex.Length;
            if (newSize == topDictIndexSize || iterationCount >= MaxIterations)
            {
                topDictIndexSize = newSize;
                break;
            }
            topDictIndexSize = newSize;
        }

        using var ms = new MemoryStream();
        ms.Write(header, 0, header.Length);
        ms.Write(nameIndex, 0, nameIndex.Length);
        ms.Write(topDictIndex, 0, topDictIndex.Length);
        ms.Write(stringIndex, 0, stringIndex.Length);
        ms.Write(globalSubrsIndex, 0, globalSubrsIndex.Length);
        ms.Write(encodingData, 0, encodingData.Length);
        ms.Write(charsetData, 0, charsetData.Length);
        ms.Write(charStringsIndex, 0, charStringsIndex.Length);

        var cffInfo = new CffInfo
        {
            NameToGid = nameToGid,
            GidToSid = sids,
            Encoding = PdfFontEncoding.Unknown,
            IsCidFont = false,
            GlyphCount = orderedCharStrings.Count,
            CodeToName = encodingVector,
            CffData = ms.ToArray()
        };

        return cffInfo;
    }

    private static GlyphCollections BuildGlyphCollections(Dictionary<PdfString, byte[]> convertedType2CharStrings)
    {
        var orderedCharStrings = new List<byte[]>(convertedType2CharStrings.Count);
        var sids = new ushort[convertedType2CharStrings.Count];
        var customStrings = new List<byte[]>(convertedType2CharStrings.Count);
        var nameToGid = new Dictionary<PdfString, ushort>(convertedType2CharStrings.Count);
        ushort nextSid = FirstCustomSid;
        int gid = 0;
        foreach (var kvp in convertedType2CharStrings)
        {
            PdfString name = kvp.Key;
            byte[] program = kvp.Value;
            orderedCharStrings.Add(program);
            if (name.IsEmpty)
            {
                sids[gid] = 0;
            }
            else
            {
                sids[gid] = nextSid;
                customStrings.Add(name.Value.ToArray());
                nextSid++;
            }
            nameToGid[name] = (ushort)gid;
            gid++;
        }
        return new GlyphCollections
        {
            OrderedCharStrings = orderedCharStrings,
            Sids = sids,
            CustomStrings = customStrings,
            NameToGid = nameToGid
        };
    }

    private static byte[] BuildCustomEncoding(int glyphCount)
    {
        if (glyphCount <= 1)
        {
            return [0, 0]; // Format0, nCodes =0.
        }
        int codeCount = glyphCount - 1; // Number of codes actually stored (exclude code0).
        using var ms = new MemoryStream();
        ms.WriteByte(0); // format0.
        ms.WriteByte((byte)codeCount);
        for (int code = 1; code < glyphCount; code++)
        {
            ms.WriteByte((byte)code);
        }
        return ms.ToArray();
    }

    private static byte[] BuildCharsetFormat0(ushort[] sids)
    {
        if (sids == null || sids.Length <= 1)
        {
            return [0];
        }
        using var ms = new MemoryStream();
        ms.WriteByte(0); // format0.
        for (int gid = 1; gid < sids.Length; gid++)
        {
            ushort sid = sids[gid];
            ms.WriteByte((byte)(sid >> 8));
            ms.WriteByte((byte)(sid & 0xFF));
        }
        return ms.ToArray();
    }

    private static byte[] BuildTopDict(
        float[] fontBBox,
        float[] fontMatrix,
        int encodingOffset,
        int charsetOffset,
        int charStringsOffset,
        int privateSize,
        int privateOffset)
    {
        using var ms = new MemoryStream();

        // FontBBox
        for (int i = 0; i < 4; i++)
        {
            CffNumberConverter.EncodeDictFloat(ms, fontBBox[i]);
        }
        ms.WriteByte(5);

        // FontMatrix
        for (int i = 0; i < 6; i++)
        {
            CffNumberConverter.EncodeDictFloat(ms, fontMatrix[i]);
        }
        ms.WriteByte(12);
        ms.WriteByte(7);

        // Encoding
        CffNumberConverter.EncodeDictInteger(ms, encodingOffset);
        ms.WriteByte(13);

        // Charset
        CffNumberConverter.EncodeDictInteger(ms, charsetOffset);
        ms.WriteByte(15);

        // CharStrings
        CffNumberConverter.EncodeDictInteger(ms, charStringsOffset);
        ms.WriteByte(17);

        // Private
        CffNumberConverter.EncodeDictInteger(ms, privateSize);
        CffNumberConverter.EncodeDictInteger(ms, privateOffset);
        ms.WriteByte(18);

        return ms.ToArray();
    }

    private struct GlyphCollections
    {
        public List<byte[]> OrderedCharStrings;
        public ushort[] Sids;
        public List<byte[]> CustomStrings;
        public Dictionary<PdfString, ushort> NameToGid;
    }
}
