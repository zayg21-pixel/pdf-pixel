using System;
using System.Text;
using PdfReader.Parsing;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Fonts.Mapping;

/// <summary>
/// Parser for PDF CMaps used in CID fonts.
/// </summary>
public static class PdfCMapParser
{        
    public static PdfCMap ParseCMapFromContext(ref PdfParseContext context, PdfDocument document)
    {
        var cmap = new PdfCMap();
        var parser = new PdfParser(context, document, allowReferences: false, decrypt: false);
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

            if (value.Type != PdfValueType.String)
            {
                continue;
            }

            var startBytes = value.AsStringBytes().Span;
            var endValue = parser.ReadNextValue();
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
                SliceBom(ref unicodeBytes);

                // Decode starting Unicode sequence (one or two UTF-16 code units) into scalar.
                string startUnicodeFull = Encoding.BigEndianUnicode.GetString(unicodeBytes);
                int baseScalar;
                if (startUnicodeFull.Length == 1)
                {
                    baseScalar = startUnicodeFull[0];
                }
                else if (startUnicodeFull.Length == 2 && char.IsSurrogatePair(startUnicodeFull[0], startUnicodeFull[1]))
                {
                    baseScalar = char.ConvertToUtf32(startUnicodeFull[0], startUnicodeFull[1]);
                }
                else
                {
                    // Multi-codepoint sequence: map only first code to full string per spec semantics.
                    cmap.AddMapping(new PdfCharacterCode(value.AsStringBytes()), startUnicodeFull);
                    continue;
                }

                int codeLength = startBytes.Length;
                uint codeStart = PdfCharacterCode.UnpackBigEndianToUInt(startBytes);
                uint codeEnd = PdfCharacterCode.UnpackBigEndianToUInt(endBytes);
                int offset = 0;
                for (uint current = codeStart; current <= codeEnd; current++, offset++)
                {
                    int scalar = baseScalar + offset;
                    if (scalar > 0x10FFFF)
                    {
                        break;
                    }

                    string unicode = char.ConvertFromUtf32(scalar);
                    var packed = PdfCharacterCode.PackUIntToBigEndian(current, codeLength);
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

    private static void SliceBom(ref ReadOnlySpan<byte> hex)
    {
        // Strip UTF-16BE BOM if present (FE FF) per PDF spec 9.10.3.
        if (hex.Length >= 2 && hex[0] == 0xFE && hex[1] == 0xFF)
        {
            hex = hex.Slice(2);
        }
    }

    // Per ISO 32000-1:2008 Section 9.10.3, Unicode values in ToUnicode CMaps
    // are encoded as UTF-16BE (big-endian UTF-16) without BOM.
    private static string ParseBytesToUnicode(ReadOnlySpan<byte> hex)
    {
        SliceBom(ref hex);
        return Encoding.BigEndianUnicode.GetString(hex);
    }

    private static bool IsSentinelFFFF(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length == 2 && bytes[0] == 0xFF && bytes[1] == 0xFF;
    }
}