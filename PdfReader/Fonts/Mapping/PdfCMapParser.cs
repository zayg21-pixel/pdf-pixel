using System;
using System.Text;
using PdfReader.Parsing;
using PdfReader.Models;
using PdfReader.Text;

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
            var parser = new PdfParser(ref context, document, allowReferences: false);
            IPdfValue value;

            while ((value = parser.ReadNextValue()) != null)
            {
                if (value.Type != PdfValueType.Operator)
                {
                    continue;
                }

                PdfCMapTokenType tokenType = value.AsString().AsEnum<PdfCMapTokenType>();
                switch (tokenType)
                {
                    case PdfCMapTokenType.BeginBfChar:
                        ParseBfCharMappings(ref parser, cmap, document);
                        break;
                    case PdfCMapTokenType.BeginBfRange:
                        ParseBfRangeMappings(ref parser, cmap, document);
                        break;
                    case PdfCMapTokenType.BeginCidChar:
                        ParseCidCharMappings(ref parser, cmap, document);
                        break;
                    case PdfCMapTokenType.BeginCidRange:
                        ParseCidRangeMappings(ref parser, cmap, document);
                        break;
                    case PdfCMapTokenType.BeginCodespaceRange:
                        ParseCodespaceRangesInternal(ref parser, cmap, document);
                        break;
                    case PdfCMapTokenType.UseCMap:
                        ResolveAndMergeUseCMap(ref parser, document, cmap);
                        break;
                }
            }
            return cmap;
        }
        
        private static void ResolveAndMergeUseCMap(ref PdfParser parser, PdfDocument document, PdfCMap target)
        {
            var cmapName = parser.ReadNextValue().AsName();

            // Prefer cached by name when available
            if (cmapName.IsEmpty)
            {
                return;
            }

            var cmap = document.GetCmap(cmapName);
            target.MergeFrom(cmap, overwriteExisting: false);
            return;
        }

        private static void ParseBfCharMappings(ref PdfParser parser, PdfCMap cmap, PdfDocument document)
        {
            IPdfValue value;

            while ((value = parser.ReadNextValue()) != null)
            {
                switch (value.Type)
                {
                    case PdfValueType.Operator:
                    {
                        var token = value.AsString().AsEnum<PdfCMapTokenType>();
                        if (token == PdfCMapTokenType.EndBfChar)
                        {
                            return;
                        }

                        break;
                    }
                    case PdfValueType.String:
                    {
                        var code = new PdfCharacterCode(value.AsStringBytes());
                        var unicodeBytes = parser.ReadNextValue().AsStringBytes().Span;

                        if (!IsSentinelFFFF(unicodeBytes))
                        {
                            cmap.AddMapping(code, ParseBytesToUnicode(unicodeBytes));
                        }

                        break;
                    }
                }
            }
        }
        
        private static void ParseBfRangeMappings(ref PdfParser parser, PdfCMap cmap, PdfDocument document)
        {
            IPdfValue value;
            while ((value = parser.ReadNextValue()) != null)
            {
                if (value.Type == PdfValueType.Operator)
                {
                    var token = value.AsString().AsEnum<PdfCMapTokenType>();
                    if (token == PdfCMapTokenType.EndBfRange)
                    {
                        return;
                    }
                    continue;
                }

                if (value.Type == PdfValueType.String)
                {
                    var startBytes = value.AsStringBytes().Span;
                    var endValue = parser.ReadNextValue();

                    if (endValue == null)
                    {
                        continue;
                    }
                    var endBytes = endValue.AsStringBytes().Span;

                    var thirdValue = parser.ReadNextValue();

                    if (thirdValue == null)
                    {
                        continue;
                    }

                    if (thirdValue.Type == PdfValueType.String)
                    {
                        var unicodeBytes = thirdValue.AsStringBytes().Span;

                        if (IsSentinelFFFF(unicodeBytes))
                        {
                            continue;
                        }

                        // Sequential form: iterate codes explicitly instead of calling AddRangeMapping.
                        uint startCodePoint = ToUnicodeCodePoint(unicodeBytes);
                        int codeLength = startBytes.Length;
                        uint codeStart = PdfCharacterCode.UnpackBigEndianToUInt(startBytes);
                        uint codeEnd = PdfCharacterCode.UnpackBigEndianToUInt(endBytes);
                        int offset = 0;
                        for (uint current = codeStart; current <= codeEnd; current++, offset++)
                        {
                            var packed = PdfCharacterCode.PackUIntToBigEndian(current, codeLength);
                            string unicode = char.ConvertFromUtf32((int)(startCodePoint + offset));
                            cmap.AddMapping(new PdfCharacterCode(packed), unicode);
                        }

                    }
                    else if (thirdValue.Type == PdfValueType.Array)
                    {
                        var array = thirdValue.AsArray();

                        int codeLength = startBytes.Length;
                        uint codeStart = PdfCharacterCode.UnpackBigEndianToUInt(startBytes);
                        uint codeEnd = PdfCharacterCode.UnpackBigEndianToUInt(endBytes);
                        uint codeCurrent = codeStart;

                        for (int arrayIndex = 0; arrayIndex < array.Count && codeCurrent <= codeEnd; arrayIndex++, codeCurrent++)
                        {
                            var arrayItem = array.GetValue(arrayIndex);
                            var hex = arrayItem.AsStringBytes().Span;

                            if (IsSentinelFFFF(hex))
                            {
                                continue;
                            }

                            string unicode = ParseBytesToUnicode(hex);

                            var codeBytes = PdfCharacterCode.PackUIntToBigEndian(codeCurrent, codeLength);
                            cmap.AddMapping(new PdfCharacterCode(codeBytes), unicode);
                        }
                    }
                }
            }
        }

        private static void ParseCodespaceRangesInternal(ref PdfParser parser, PdfCMap cmap, PdfDocument document)
        {

            IPdfValue value;
            while ((value = parser.ReadNextValue()) != null)
            {
                switch (value.Type)
                {
                    case PdfValueType.Operator:
                    {
                        var token = value.AsString().AsEnum<PdfCMapTokenType>();
                        if (token == PdfCMapTokenType.EndCodespaceRange)
                        {
                            return;
                        }

                        continue;
                    }

                    case PdfValueType.String:
                    {
                        var startBytes = value.AsStringBytes().Span;
                        var endValue = parser.ReadNextValue();
                        var endBytes = endValue.AsStringBytes().Span;

                        cmap.AddCodespaceRange(startBytes, endBytes);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Parse begincidchar block: maps code (hex string) to CID (integer), then to Unicode string.
        /// </summary>
        private static void ParseCidCharMappings(ref PdfParser parser, PdfCMap cmap, PdfDocument document)
        {
            IPdfValue value;
            while ((value = parser.ReadNextValue()) != null)
            {
                switch (value.Type)
                {
                    case PdfValueType.Operator:
                    {
                        var token = value.AsString().AsEnum<PdfCMapTokenType>();
                        if (token == PdfCMapTokenType.EndCidChar)
                        {
                            return;
                        }
                        break;
                    }
                    case PdfValueType.String:
                    {
                        var codeBytes = value.AsStringBytes();
                        var cidValue = parser.ReadNextValue();
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
        private static void ParseCidRangeMappings(ref PdfParser parser, PdfCMap cmap, PdfDocument document)
        {
            IPdfValue value;
            while ((value = parser.ReadNextValue()) != null)
            {
                switch (value.Type)
                {
                    case PdfValueType.Operator:
                    {
                        var token = value.AsString().AsEnum<PdfCMapTokenType>();
                        if (token == PdfCMapTokenType.EndCidRange)
                        {
                            return;
                        }
                        break;
                    }
                    case PdfValueType.String:
                    {
                        var startBytes = value.AsStringBytes().Span;
                        var endValue = parser.ReadNextValue();
                        var endBytes = endValue.AsStringBytes().Span;

                        var cidValue = parser.ReadNextValue();
                        var firstCid = cidValue.AsInteger();

                        cmap.AddCidRangeMapping(startBytes, endBytes, firstCid);
                        break;
                    }
                }
            }
        }

        private static uint ToUnicodeCodePoint(ReadOnlySpan<byte> hex)
        {
            // Strip UTF-16BE BOM if present (FE FF) per PDF spec 9.10.3.
            if (hex.Length >= 2 && hex[0] == 0xFE && hex[1] == 0xFF)
            {
                hex = hex.Slice(2);
            }

            // Need at least one 16-bit code unit.
            if (hex.Length < 2)
            {
                return 0u;
            }

            int firstUnit = (hex[0] << 8) | hex[1];

            // Handle surrogate pair to form a single scalar value (non-BMP).
            if (firstUnit >= 0xD800 && firstUnit <= 0xDBFF)
            {
                // High surrogate; require low surrogate code unit.
                if (hex.Length >= 4)
                {
                    int secondUnit = (hex[2] << 8) | hex[3];
                    if (secondUnit >= 0xDC00 && secondUnit <= 0xDFFF)
                    {
                        int high = firstUnit - 0xD800;
                        int low = secondUnit - 0xDC00;
                        uint codePoint = (uint)((high << 10) + low + 0x10000);
                        return codePoint;
                    }
                }
                // Invalid surrogate sequence; fall back to returning first unit value.
            }

            return (uint)firstUnit;
        }

        // Per ISO 32000-1:2008 Section 9.10.3, Unicode values in ToUnicode CMaps
        // are encoded as UTF-16BE (big-endian UTF-16) without BOM.
        private static string ParseBytesToUnicode(ReadOnlySpan<byte> hex)
        {
            if (hex.Length >= 2 && hex[0] == 0xFE && hex[1] == 0xFF)
            {
                hex = hex.Slice(2);
            }
            return Encoding.BigEndianUnicode.GetString(hex);
        }

        private static bool IsSentinelFFFF(ReadOnlySpan<byte> bytes)
        {
            return bytes.Length == 2 && bytes[0] == 0xFF && bytes[1] == 0xFF;
        }
    }
}