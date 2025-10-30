using System;
using System.Text;
using PdfReader.Parsing;
using PdfReader.Models;

namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// Parser for PDF CMaps used in CID fonts.
    /// </summary>
    public static class PdfCMapParser
    {        
        public static PdfCMap ParseCMapFromContext(ref PdfParseContext context, PdfDocument document)
        {
            var cmap = new PdfCMap();
            IPdfValue value;

            while ((value = PdfParsers.ParsePdfValue(ref context, document)) != null)
            {
                if (value.Type != PdfValueType.Operator)
                {
                    continue;
                }

                switch (value.AsString())
                {
                    case PdfTokens.BeginBfCharKey:
                        ParseBfCharMappings(ref context, cmap, document);
                        break;
                    case PdfTokens.BeginBfRangeKey:
                        ParseBfRangeMappings(ref context, cmap, document);
                        break;
                    case PdfTokens.BeginCidCharKey:
                        ParseCidCharMappings(ref context, cmap, document);
                        break;
                    case PdfTokens.BeginCidRangeKey:
                        ParseCidRangeMappings(ref context, cmap, document);
                        break;
                    case PdfTokens.BeginCodespaceRangeKey:
                        ParseCodespaceRangesInternal(ref context, cmap, document);
                        break;
                    case PdfTokens.UseCMapKey:
                        ResolveAndMergeUseCMap(ref context, document, cmap);
                        break;
                }
            }
            return cmap;
        }
        
        private static void ResolveAndMergeUseCMap(ref PdfParseContext context, PdfDocument document, PdfCMap target)
        {
            var arg = PdfParsers.ParsePdfValue(ref context, document);
            if (arg == null)
            {
                return;
            }

            string cmapName = null;

            if (arg.Type == PdfValueType.Name)
            {
                cmapName = arg.AsName();
            }

            // Prefer cached by name when available
            if (!string.IsNullOrEmpty(cmapName))
            {
                var cmap = document.GetCmap(cmapName);
                target.MergeFrom(cmap, overwriteExisting: false);
                return;
            }
        }

        private static void ParseBfCharMappings(ref PdfParseContext context, PdfCMap cmap, PdfDocument document)
        {
            IPdfValue value;

            while ((value = PdfParsers.ParsePdfValue(ref context, document)) != null)
            {
                switch (value.Type)
                {
                    case PdfValueType.Operator:
                    {
                        string operatorValue = value.AsString();

                        if (operatorValue == PdfTokens.EndBfCharKey)
                        {
                            return;
                        }

                        break;
                    }
                    case PdfValueType.String:
                    {
                        var code = new PdfCharacterCode(value.AsStringBytes());
                        var unicodeBytes = PdfParsers.ParsePdfValue(ref context, document).AsStringBytes();

                        if (unicodeBytes == null)
                        {
                            continue;
                        }

                        if (!IsSentinelFFFF(unicodeBytes))
                        {
                            cmap.AddMapping(code, ParseBytesToUnicode(unicodeBytes));
                        }

                        break;
                    }
                }
            }
        }
        
        private static void ParseBfRangeMappings(ref PdfParseContext context, PdfCMap cmap, PdfDocument document)
        {
            IPdfValue value;
            while ((value = PdfParsers.ParsePdfValue(ref context, document)) != null)
            {
                if (value.Type == PdfValueType.Operator)
                {
                    string operatorValue = value.AsString();
                    if (operatorValue == PdfTokens.EndBfRangeKey)
                    {
                        return;
                    }
                    continue;
                }

                if (value.Type == PdfValueType.String)
                {
                    var startBytes = value.AsStringBytes();
                    var endValue = PdfParsers.ParsePdfValue(ref context, document);

                    if (endValue == null)
                    {
                        continue;
                    }
                    var endBytes = endValue.AsStringBytes();

                    var thirdValue = PdfParsers.ParsePdfValue(ref context, document);

                    if (thirdValue == null)
                    {
                        continue;
                    }

                    if (thirdValue.Type == PdfValueType.String)
                    {
                        var unicodeBytes = thirdValue.AsStringBytes();

                        if (unicodeBytes == null || IsSentinelFFFF(unicodeBytes))
                        {
                            continue;
                        }

                        int startCodePoint = FirstCodePointFromUtf16BE(unicodeBytes);
                        cmap.AddRangeMapping(startBytes, endBytes, startCodePoint);

                    }
                    else if (thirdValue.Type == PdfValueType.Array)
                    {
                        var array = thirdValue.AsArray();

                        int codeLength = startBytes.Length;
                        uint codeStart = BytesToUIntBE(startBytes);
                        uint codeEnd = BytesToUIntBE(endBytes);
                        uint codeCurrent = codeStart;

                        for (int arrayIndex = 0; arrayIndex < array.Count && codeCurrent <= codeEnd; arrayIndex++, codeCurrent++)
                        {
                            var arrayItem = array.GetValue(arrayIndex);
                            var hex = arrayItem.AsStringBytes();

                            if (hex == null || IsSentinelFFFF(hex))
                            {
                                continue;
                            }

                            string unicode = ParseBytesToUnicode(hex);

                            var codeBytes = UIntToBytesBE(codeCurrent, codeLength);
                            cmap.AddMapping(new PdfCharacterCode(codeBytes), unicode);
                        }
                    }
                }
            }
        }

        private static void ParseCodespaceRangesInternal(ref PdfParseContext context, PdfCMap cmap, PdfDocument document)
        {

            IPdfValue value;
            while ((value = PdfParsers.ParsePdfValue(ref context, document)) != null)
            {
                switch (value.Type)
                {
                    case PdfValueType.Operator:
                    {
                        string operatorValue = value.AsString();
                        if (operatorValue == PdfTokens.EndCodespaceRangeKey)
                        {
                            return;
                        }

                        continue;
                    }

                    case PdfValueType.String:
                    {
                        var startBytes = value.AsStringBytes();
                        var endValue = PdfParsers.ParsePdfValue(ref context, document);
                        var endBytes = endValue.AsStringBytes();

                        cmap.AddCodespaceRange(startBytes, endBytes);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Parse begincidchar block: maps code (hex string) to CID (integer), then to Unicode string.
        /// </summary>
        private static void ParseCidCharMappings(ref PdfParseContext context, PdfCMap cmap, PdfDocument document)
        {
            IPdfValue value;
            while ((value = PdfParsers.ParsePdfValue(ref context, document)) != null)
            {
                switch (value.Type)
                {
                    case PdfValueType.Operator:
                    {
                        if (value.AsString() == PdfTokens.EndCidCharKey)
                        {
                            return;
                        }
                        break;
                    }
                    case PdfValueType.String:
                    {
                        var codeBytes = value.AsStringBytes();
                        var cidValue = PdfParsers.ParsePdfValue(ref context, document);
                        var cid = cidValue.AsInteger();
                        cmap.AddCidMapping(new PdfCharacterCode(codeBytes), cid);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Parse begincidrange block: maps code range to sequential CIDs, then to Unicode strings.
        /// </summary>
        private static void ParseCidRangeMappings(ref PdfParseContext context, PdfCMap cmap, PdfDocument document)
        {
            IPdfValue value;
            while ((value = PdfParsers.ParsePdfValue(ref context, document)) != null)
            {
                switch (value.Type)
                {
                    case PdfValueType.Operator:
                    {
                        if (value.AsString() == PdfTokens.EndCidRangeKey)
                        {
                            return;
                        }
                        break;
                    }
                    case PdfValueType.String:
                    {
                        var startBytes = value.AsStringBytes();
                        var endValue = PdfParsers.ParsePdfValue(ref context, document);
                        var endBytes = endValue.AsStringBytes();

                        var cidValue = PdfParsers.ParsePdfValue(ref context, document);
                        var firstCid = cidValue.AsInteger();

                        cmap.AddCidRangeMapping(startBytes, endBytes, firstCid);
                        break;
                    }
                }
            }
        }

        // Per ISO 32000-1:2008 Section 9.10.3, Unicode values in ToUnicode CMaps
        // are encoded as UTF-16BE (big-endian UTF-16) without BOM.
        private static string ParseBytesToUnicode(byte[] hex)
        {
            if (hex != null && hex.Length >= 2 && hex[0] == 0xFE && hex[1] == 0xFF)
            {
                var withoutBom = new byte[hex.Length - 2];
                Buffer.BlockCopy(hex, 2, withoutBom, 0, withoutBom.Length);
                hex = withoutBom;
            }
            return Encoding.BigEndianUnicode.GetString(hex ?? Array.Empty<byte>());
        }

        private static int FirstCodePointFromUtf16BE(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return 0;
            }
            var s = ParseBytesToUnicode(bytes);
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }
            if (char.IsHighSurrogate(s[0]) && s.Length >= 2 && char.IsLowSurrogate(s[1]))
            {
                return char.ConvertToUtf32(s[0], s[1]);
            }
            return s[0];
        }

        private static bool IsSentinelFFFF(byte[] bytes)
        {
            return bytes != null && bytes.Length == 2 && bytes[0] == 0xFF && bytes[1] == 0xFF;
        }

        private static uint BytesToUIntBE(byte[] bytes)
        {
            uint v = 0u;
            for (int i = 0; i < bytes.Length; i++)
            {
                v = v << 8 | bytes[i];
            }
            return v;
        }

        private static byte[] UIntToBytesBE(uint value, int length)
        {
            var bytes = new byte[length];
            for (int i = length - 1; i >= 0; i--)
            {
                bytes[i] = (byte)(value & 0xFF);
                value >>= 8;
            }
            return bytes;
        }
    }
}