using PdfReader.Fonts.Types;
using PdfReader.PostScript.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PdfReader.Fonts.PsFont
{
    internal static class Type1Interpreter
    {
        public static byte[] GenerateCffFontData(PostScriptDictionary fontDictionary, PdfFontDescriptor decriptor)
        {
            if (fontDictionary == null)
            {
                return null;
            }
            if (decriptor == null)
            {
                return null;
            }

            Dictionary<PdfReader.Models.PdfString, byte[]> type1CharStrings = PsFontDictionaryParser.GetCharStrings(fontDictionary);
            Dictionary<int, byte[]> type1Subrs = PsFontDictionaryParser.GetSubroutines(fontDictionary); // Source local subrs for flattening.
            var fontMatrix = PsFontDictionaryParser.GetFontMatrix(fontDictionary); // TODO: Emit FontMatrix (127) real numbers in Top DICT.

            // Removed heuristic that injected an empty .notdef when missing.

            float[] effectiveFontBBox = PsFontDictionaryParser.GetFontBBox(fontDictionary);

            if (effectiveFontBBox == null)
            {
                // Spec requires FontBBox for non-Standard14 embedded Type1. We abort (caller can decide to synthesize bounds first).
                return null;
            }

            string fontName = GetFontName(fontDictionary);
            if (string.IsNullOrEmpty(fontName) && decriptor.FontName.ToString() != null)
            {
                fontName = decriptor.FontName.ToString();
            }
            if (string.IsNullOrEmpty(fontName))
            {
                fontName = "UnnamedFont";
            }

            var parameters = new Type1ConverterContext { LocalSubrs = type1Subrs };

            // Flatten conversion: inline subroutines for each glyph (TODO: proper bias / full operator translation).
            Dictionary<PdfReader.Models.PdfString, byte[]> type2CharStrings = Type1CharStringConverter.ConvertAllCharStringsToType2Flatten(type1CharStrings, parameters);

            // Get Type1 /Encoding vector to drive deterministic code->GID ordering.
            PdfReader.Models.PdfString[] encodingVector = PsFontDictionaryParser.GetEncodingVector(fontDictionary);
            byte[] charStringsIndex = BuildCharStringsIndexByEncoding(encodingVector, type2CharStrings);

            byte[] header = BuildHeader();
            byte[] nameIndex = BuildNameIndex(fontName);
            byte[] stringIndex = BuildEmptyIndex();
            byte[] globalSubrsIndex = BuildEmptyIndex(); // Global subrs empty (TODO: support when needed)
            // Local subrs omitted after flattening.
            byte[] localSubrsIndex = Array.Empty<byte>();
            byte[] privateDict = BuildPrivateDict(false); // No LocalSubrs operator since we flattened.

            byte[] topDictIndex = Array.Empty<byte>();
            int topDictIndexSize = 0;
            int iterationCount = 0;
            const int MaxIterations = 4;
            int charStringsOffset = 0;
            int privateDictOffset = 0;

            while (true)
            {
                iterationCount++;
                int offset = 0;
                offset += header.Length;
                offset += nameIndex.Length;
                int topDictIndexOffset = offset;
                int stringIndexOffset = topDictIndexOffset + topDictIndexSize;
                int globalSubrsOffset = stringIndexOffset + stringIndex.Length;
                charStringsOffset = globalSubrsOffset + globalSubrsIndex.Length;
                privateDictOffset = charStringsOffset + charStringsIndex.Length;
                byte[] topDictData = BuildTopDict(effectiveFontBBox, charStringsOffset, privateDict.Length, privateDictOffset);
                topDictIndex = BuildSingleObjectIndex(topDictData);
                int newSize = topDictIndex.Length;
                if (newSize == topDictIndexSize || iterationCount >= MaxIterations)
                {
                    topDictIndexSize = newSize;
                    break;
                }
                topDictIndexSize = newSize;
            }

            using (var ms = new MemoryStream())
            {
                ms.Write(header, 0, header.Length);
                ms.Write(nameIndex, 0, nameIndex.Length);
                ms.Write(topDictIndex, 0, topDictIndex.Length);
                ms.Write(stringIndex, 0, stringIndex.Length);
                ms.Write(globalSubrsIndex, 0, globalSubrsIndex.Length);
                ms.Write(charStringsIndex, 0, charStringsIndex.Length);
                ms.Write(privateDict, 0, privateDict.Length);
                // localSubrsIndex intentionally omitted (flattened)
                return ms.ToArray();
            }
        }

        #region Extraction helpers
        private static string GetFontName(PostScriptDictionary dict)
        {
            if (dict == null)
            {
                return null;
            }
            if (dict.Entries.TryGetValue("FontName", out PostScriptToken token))
            {
                if (token is PostScriptLiteralName ln)
                {
                    return ln.Name;
                }
                if (token is PostScriptString ps)
                {
                    return ps.Value;
                }
            }
            return null;
        }

        #endregion

        #region CFF building helpers
        private static byte[] BuildHeader()
        {
            // major=1, minor=0, headerSize=4, offSize=1 (minimal).
            return new byte[] { 1, 0, 4, 1 };
        }

        private static byte[] BuildNameIndex(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
            {
                fontName = "UnnamedFont";
            }
            byte[] nameBytes = Encoding.ASCII.GetBytes(fontName);
            return BuildSingleObjectIndex(nameBytes);
        }

        private static byte[] BuildEmptyIndex()
        {
            return new byte[] { 0, 0 };
        }

        private static byte[] BuildSingleObjectIndex(byte[] data)
        {
            int endOffset = data.Length + 1;
            byte offSize = endOffset <= 0xFF ? (byte)1 : endOffset <= 0xFFFF ? (byte)2 : endOffset <= 0xFFFFFF ? (byte)3 : (byte)4;
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0);
                ms.WriteByte(1);
                ms.WriteByte(offSize);
                WriteOffset(ms, 1, offSize);
                WriteOffset(ms, endOffset, offSize);
                ms.Write(data, 0, data.Length);
                return ms.ToArray();
            }
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

        private static byte[] BuildCharStringsIndexByEncoding(PdfReader.Models.PdfString[] encodingVector, Dictionary<PdfReader.Models.PdfString, byte[]> converted)
        {
            // Direct mapping. Each entry in the encoding vector corresponds1:1 to an INDEX object.
            var ordered = new List<byte[]>(encodingVector.Length);
            for (int i = 0; i < encodingVector.Length; i++)
            {
                var name = encodingVector[i];
                if (converted.TryGetValue(name, out byte[] program))
                {
                    ordered.Add(program); // program expected non-null per converter contract.
                }
                else
                {
                    ordered.Add(Array.Empty<byte>());
                }
            }
            return BuildIndex(ordered);
        }

        private static byte[] BuildIndex(List<byte[]> objects)
        {
            if (objects == null || objects.Count == 0)
            {
                return new byte[] { 0, 0 };
            }
            int count = objects.Count;
            int current = 1;
            var offsets = new List<int>(count + 1) { 1 };
            foreach (byte[] obj in objects)
            {
                current += obj.Length; // obj never null: always program or Array.Empty<byte>().
                offsets.Add(current);
            }
            int maxOffset = offsets[offsets.Count - 1];
            byte offSize = maxOffset <= 0xFF ? (byte)1 : maxOffset <= 0xFFFF ? (byte)2 : maxOffset <= 0xFFFFFF ? (byte)3 : (byte)4;
            using (var ms = new MemoryStream())
            {
                ms.WriteByte((byte)(count >> 8));
                ms.WriteByte((byte)(count & 0xFF));
                ms.WriteByte(offSize);
                foreach (int off in offsets)
                {
                    WriteOffset(ms, off, offSize);
                }
                foreach (byte[] obj in objects)
                {
                    ms.Write(obj, 0, obj.Length);
                }
                return ms.ToArray();
            }
        }

        private static byte[] BuildPrivateDict(bool hasLocalSubrs)
        {
            if (!hasLocalSubrs)
            {
                return Array.Empty<byte>();
            }
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(139); //0 offset to local subrs
                ms.WriteByte(12);
                ms.WriteByte(19); // LocalSubrs
                return ms.ToArray();
            }
        }

        private static byte[] BuildTopDict(float[] fontBBox,
        int charStringsOffset,
        int privateSize,
        int privateOffset)
        {
            using (var ms = new MemoryStream())
            {
                if (fontBBox != null && fontBBox.Length == 4)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        EncodeDictInteger(ms, (int)Math.Round(fontBBox[i]));
                    }
                    ms.WriteByte(5); // FontBBox
                }
                EncodeDictInteger(ms, charStringsOffset);
                ms.WriteByte(17); // CharStrings
                EncodeDictInteger(ms, privateSize);
                EncodeDictInteger(ms, privateOffset);
                ms.WriteByte(18); // Private
                return ms.ToArray();
            }
        }

        private static void EncodeDictInteger(Stream s, int value)
        {
            // Correct CFF DICT integer encoding (no255 marker; use28 or29 for large values).
            if (value >= -107 && value <= 107)
            {
                s.WriteByte((byte)(value + 139));
                return;
            }
            if (value >= 108 && value <= 1131)
            {
                int v = value - 108;
                int b1 = v / 256;
                int b2 = v % 256;
                s.WriteByte((byte)(247 + b1));
                s.WriteByte((byte)b2);
                return;
            }
            if (value >= -1131 && value <= -108)
            {
                int v = -value - 108;
                int b1 = v / 256;
                int b2 = v % 256;
                s.WriteByte((byte)(251 + b1));
                s.WriteByte((byte)b2);
                return;
            }
            if (value >= -32768 && value <= 32767)
            {
                //16-bit integer encoding:28 + big-endian int16
                s.WriteByte(28);
                unchecked
                {
                    s.WriteByte((byte)((value >> 8) & 0xFF));
                    s.WriteByte((byte)(value & 0xFF));
                }
                return;
            }
            //32-bit integer encoding:29 + big-endian int32
            s.WriteByte(29);
            unchecked
            {
                s.WriteByte((byte)((value >> 24) & 0xFF));
                s.WriteByte((byte)((value >> 16) & 0xFF));
                s.WriteByte((byte)((value >> 8) & 0xFF));
                s.WriteByte((byte)(value & 0xFF));
            }
        }
        #endregion
    }
}
