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

    public static byte[] GenerateCffFontDataFromDictionary(PostScriptDictionary fontDictionary, PdfSimpleFont font)
    {
        if (fontDictionary == null)
        {
            return null;
        }
        if (font?.FontDescriptor == null)
        {
            return null;
        }

        PdfFontDescriptor descriptor = font.FontDescriptor;

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
        Array.Resize(ref encodingVector, 255);

        foreach (var difference in font.Differences)
        {
            var code = difference.Key;
            if (code < encodingVector.Length && type2CharStrings.ContainsKey(difference.Value))
            {
                encodingVector[code] = difference.Value;
            }
        }

        // TODO: we need to not only merge differences, but also ensure that font gets correct encoding data!
        // we also need to merge with font's base encoding if it's defined. Basically, override encoding vector if font defines it's own!

        var glyphCollections = BuildGlyphCollections(encodingVector, type2CharStrings);
        List<byte[]> orderedCharStrings = glyphCollections.OrderedCharStrings;
        int[] sids = glyphCollections.Sids;
        List<byte[]> customStrings = glyphCollections.CustomStrings;

        byte[] charStringsIndex = BuildIndex(orderedCharStrings);
        byte[] stringIndex = BuildIndex(customStrings); // May be empty.
        byte[] charsetData = BuildCharsetFormat0(sids); // Skips GID0 when writing.

        byte[] header = BuildHeader();
        byte[] nameIndex = BuildSingleObjectIndex(Encoding.ASCII.GetBytes(fontName));
        byte[] globalSubrsIndex = BuildEmptyIndex();
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
            topDictIndex = BuildSingleObjectIndex(topDictData);
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
        return ms.ToArray();
    }

    private static byte[] BuildHeader()
    {
        // major=1, minor=0, headerSize=4, offSize=1 (minimal).
        return [1, 0, 4, 1];
    }

    private static byte[] BuildEmptyIndex()
    {
        return [0, 0];
    }

    private static byte[] BuildSingleObjectIndex(byte[] data)
    {
        int endOffset = data.Length + 1; // First offset is always1.
        byte offSize = endOffset <= 0xFF ? (byte)1 : endOffset <= 0xFFFF ? (byte)2 : endOffset <= 0xFFFFFF ? (byte)3 : (byte)4;

        using var ms = new MemoryStream();
        ms.WriteByte(0); // count high byte (0 for1 object).
        ms.WriteByte(1); // count low byte.
        ms.WriteByte(offSize);
        WriteOffset(ms, 1, offSize); // First object starts at1.
        WriteOffset(ms, endOffset, offSize); // End of object.
        ms.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    private static void WriteOffset(Stream s, int value, int size)
    {
        for (int i = size - 1; i >= 0; i--)
        {
            int shift = i * 8;
            byte b = (byte)((value >> shift) & 0xFF);
            s.WriteByte(b);
        }
    }

    private static byte[] BuildIndex(List<byte[]> objects)
    {
        if (objects == null || objects.Count == 0)
        {
            return BuildEmptyIndex();
        }

        int count = objects.Count;
        int currentOffset = 1; // First object offset is1 per CFF spec.
        List<int> offsets = new List<int>(count + 1) { 1 };
        foreach (byte[] obj in objects)
        {
            currentOffset += obj.Length;
            offsets.Add(currentOffset);
        }
        int maxOffset = offsets[offsets.Count - 1];
        byte offSize = maxOffset <= 0xFF ? (byte)1 : maxOffset <= 0xFFFF ? (byte)2 : maxOffset <= 0xFFFFFF ? (byte)3 : (byte)4;

        using var ms = new MemoryStream();
        ms.WriteByte((byte)(count >> 8));
        ms.WriteByte((byte)(count & 0xFF));
        ms.WriteByte(offSize);
        foreach (int offset in offsets)
        {
            WriteOffset(ms, offset, offSize);
        }
        foreach (byte[] obj in objects)
        {
            ms.Write(obj, 0, obj.Length);
        }
        return ms.ToArray();
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

    private static (List<byte[]> OrderedCharStrings, int[] Sids, List<byte[]> CustomStrings) BuildGlyphCollections(
     PdfString[] encodingVector,
     Dictionary<PdfString, byte[]> convertedType2CharStrings)
    {
        List<byte[]> orderedCharStrings = new List<byte[]>(encodingVector.Length);
        int[] sids = new int[encodingVector.Length];
        List<byte[]> customStrings = new List<byte[]>(encodingVector.Length);
        int nextSid = FirstCustomSid;

        for (int gid = 0; gid < encodingVector.Length; gid++)
        {
            PdfString name = encodingVector[gid];

            if (convertedType2CharStrings.TryGetValue(name, out byte[] program))
            {
                orderedCharStrings.Add(program);
            }
            else
            {
                orderedCharStrings.Add(Array.Empty<byte>());
            }

            if (name.IsEmpty)
            {
                sids[gid] = 0;
                continue;
            }

            sids[gid] = nextSid;
            nextSid++;
            customStrings.Add(name.Value.ToArray());
        }

        return (orderedCharStrings, sids, customStrings);
    }

    private static byte[] BuildCharsetFormat0(int[] sids)
    {
        if (sids == null || sids.Length <= 1)
        {
            return [0];
        }
        using var ms = new MemoryStream();
        ms.WriteByte(0); // format0.
        for (int gid = 1; gid < sids.Length; gid++)
        {
            int sid = sids[gid];
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
            FontNumberConverter.EncodeDictFloat(ms, fontBBox[i]);
        }
        ms.WriteByte(5);

        // FontMatrix
        for (int i = 0; i < 6; i++)
        {
            FontNumberConverter.EncodeDictFloat(ms, fontMatrix[i]);
        }
        ms.WriteByte(12);
        ms.WriteByte(7);

        // Encoding
        FontNumberConverter.EncodeDictInteger(ms, encodingOffset);
        ms.WriteByte(13);

        // Charset
        FontNumberConverter.EncodeDictInteger(ms, charsetOffset);
        ms.WriteByte(15);

        // CharStrings
        FontNumberConverter.EncodeDictInteger(ms, charStringsOffset);
        ms.WriteByte(17);

        // Private
        FontNumberConverter.EncodeDictInteger(ms, privateSize);
        FontNumberConverter.EncodeDictInteger(ms, privateOffset);
        ms.WriteByte(18);

        return ms.ToArray();
    }
}
