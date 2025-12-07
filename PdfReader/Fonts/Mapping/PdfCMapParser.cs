using System;
using System.Text;
using PdfReader.Models;
using PdfReader.Text;
using PdfReader.PostScript;
using Microsoft.Extensions.Logging;
using PdfReader.PostScript.Tokens;
using System.Linq;
using PdfReader.Fonts.Model;

namespace PdfReader.Fonts.Mapping;

/// <summary>
/// Parser for PDF CMaps used in CID fonts.
/// </summary>
public static class PdfCMapParser
{        
    public static PdfCMap ParseCMap(ReadOnlyMemory<byte> cmapBytes, PdfDocument document)
    {
        var cmap = new PdfCMap();
        var evaluator = new PostScriptEvaluator(cmapBytes.Span, false, document.LoggerFactory.CreateLogger<PostScriptEvaluator>());

        evaluator.SetResourceValue("ProcSet", "CIDInit", new PostScriptDictionary());
        var stack = new System.Collections.Generic.Stack<PostScriptToken>();
        evaluator.EvaluateTokens(stack);

        var cmaps = evaluator.GetResourceCategory(PdfTokens.CMapKey.ToString());
        var cmapDictionary = cmaps?.Entries.FirstOrDefault().Value as PostScriptDictionary;

        if (cmapDictionary == null)
        {
            return cmap;
        }

        cmap.CidSystemInfo = GetInfo(cmapDictionary);

        if (cmapDictionary.Entries.TryGetValue("codespacerange", out var codespaceRange) && codespaceRange is PostScriptArray codespaceRangeArray)
        {
            var arrayItems = codespaceRangeArray.Elements;

            foreach (var item in arrayItems)
            {
                if (item is PostScriptArray innerArray)
                {
                    ParseCodespaceRangeMapping(innerArray.Elements, cmap);
                }
            }
        }

        if (cmapDictionary.Entries.TryGetValue("bfchar", out var bfChar) && bfChar is PostScriptArray bfCharArray)
        {
            var arrayItems = bfCharArray.Elements;

            foreach (var item in arrayItems)
            {
                if (item is PostScriptArray innerArray)
                {
                    ParseBfCharMappings(innerArray.Elements, cmap);
                }
            }
        }

        if (cmapDictionary.Entries.TryGetValue("bfrange", out var bfRange) && bfRange is PostScriptArray bfRangeArray)
        {
            var arrayItems = bfRangeArray.Elements;

            foreach (var item in arrayItems)
            {
                if (item is PostScriptArray innerArray)
                {
                    ParseBfRangeMappings(innerArray.Elements, cmap);
                }
            }

        }

        if (cmapDictionary.Entries.TryGetValue("cidchar", out var cidChar) && cidChar is PostScriptArray cidCharArray)
        {
            var arrayItems = cidCharArray.Elements;

            foreach (var item in arrayItems)
            {
                if (item is PostScriptArray innerArray)
                {
                    ParseCidCharMappings(innerArray.Elements, cmap);
                }
            }
        }

        if (cmapDictionary.Entries.TryGetValue("cidrange", out var cidRange) && cidRange is PostScriptArray cidRangeArray)
        {
            var arrayItems = cidRangeArray.Elements;

            foreach (var item in arrayItems)
            {
                if (item is PostScriptArray innerArray)
                {
                    ParseCidRangeMappings(innerArray.Elements, cmap);
                }
            }
        }

        if (cmapDictionary.Entries.TryGetValue("usecmap", out var useCMapToken) && useCMapToken is PostScriptArray useCMapArray)
        {
            foreach (var element in useCMapArray.Elements)
            {
                if (element is PostScriptLiteralName useCMapName)
                {
                    var baseCMap = document.CMapCache.GetCmap(PdfString.FromString(useCMapName.Name));
                    if (baseCMap != null)
                    {
                        cmap.MergeFrom(baseCMap);
                    }
                }
            }
        }

        if (cmapDictionary.Entries.TryGetValue("WMode", out var wmodeToken) && wmodeToken is PostScriptNumber wmodeNumber)
        {
            cmap.WMode = (CMapWMode)(int)wmodeNumber.Value;
        }

        if (cmapDictionary.Entries.TryGetValue("CMapName", out var name) && name is PostScriptLiteralName nameLiteral)
        {
            cmap.Name = PdfString.FromString(nameLiteral.Name);
        }

        return cmap;
    }

    private static PdfCidSystemInfo GetInfo(PostScriptDictionary dictionary)
    {
        if (dictionary == null)
        {
            return null;
        }

        if (dictionary.Entries.TryGetValue(PdfTokens.CidSystemInfoKey.ToString(), out var infoValue) && infoValue is PostScriptDictionary infoDictionary)
        {
            return PdfCidSystemInfo.FromPostscriptDictionary(infoDictionary);
        }

        return null;
    }

    private static void ParseCodespaceRangeMapping(PostScriptToken[] tokens, PdfCMap cmap)
    {
        for (int i = 0; i < tokens.Length;)
        {
            var startToken = tokens[i++] as PostScriptString;
            var endToken = tokens[i++] as PostScriptString;
            if (startToken == null || endToken == null)
            {
                continue;
            }
            cmap.AddCodespaceRange(startToken.Value.AsSpan(), endToken.Value.AsSpan());
        }
    }

    private static void ParseBfCharMappings(PostScriptToken[] tokens, PdfCMap cmap)
    {
        for (int i = 0; i < tokens.Length;)
        {
            var codeString = tokens[i++] as PostScriptString;
            var unicodeString = tokens[i++] as PostScriptString;

            if (codeString == null || unicodeString == null)
            {
                continue;
            }

            var code = new PdfCharacterCode(codeString.Value);

            var unicodeBytes = unicodeString.Value.AsSpan();

            if (!IsSentinelFFFF(unicodeBytes))
            {
                cmap.AddMapping(code, ParseBytesToUnicode(unicodeBytes));
            }
        }
    }

    private static void ParseBfRangeMappings(PostScriptToken[] tokens, PdfCMap cmap)
    {
        for (int i = 0; i < tokens.Length;)
        {
            // Expect: codeStart, codeEnd, unicodeOrArray
            var startToken = tokens[i++] as PostScriptString;
            var endToken = tokens[i++] as PostScriptString;
            var thirdToken = tokens[i++];

            if (startToken == null || endToken == null || thirdToken == null)
            {
                continue;
            }

            var startBytes = startToken.Value.AsSpan();
            var endBytes = endToken.Value.AsSpan();

            if (thirdToken is PostScriptString unicodeString)
            {
                ReadOnlySpan<byte> unicodeBytes = unicodeString.Value.AsSpan();
                if (IsSentinelFFFF(unicodeBytes))
                {
                    continue;
                }
                SliceBom(ref unicodeBytes);

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
                    cmap.AddMapping(new PdfCharacterCode(startToken.Value), startUnicodeFull);
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
            else if (thirdToken is PostScriptArray array)
            {
                int codeLength = startBytes.Length;
                uint codeStart = PdfCharacterCode.UnpackBigEndianToUInt(startBytes);
                uint codeEnd = PdfCharacterCode.UnpackBigEndianToUInt(endBytes);
                uint codeCurrent = codeStart;

                for (int arrayIndex = 0; arrayIndex < array.Elements.Length && codeCurrent <= codeEnd; arrayIndex++, codeCurrent++)
                {
                    var arrayItem = array.Elements[arrayIndex] as PostScriptString;
                    if (arrayItem == null)
                    {
                        continue;
                    }
                    ReadOnlySpan<byte> hex = arrayItem.Value.AsSpan();
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

    private static void ParseCidCharMappings(PostScriptToken[] tokens, PdfCMap cmap)
    {
        for (int i = 0; i < tokens.Length;)
        {
            var codeToken = tokens[i++] as PostScriptString;
            var cidToken = tokens[i++] as PostScriptNumber;
            if (codeToken == null || cidToken == null)
            {
                continue;
            }
            cmap.AddCidMapping(new PdfCharacterCode(codeToken.Value), (int)cidToken.Value);
        }
    }

    private static void ParseCidRangeMappings(PostScriptToken[] tokens, PdfCMap cmap)
    {
        for (int i = 0; i < tokens.Length;)
        {
            var startToken = tokens[i++] as PostScriptString;
            var endToken = tokens[i++] as PostScriptString;
            var firstCidToken = tokens[i++] as PostScriptNumber;
            if (startToken == null || endToken == null || firstCidToken == null)
            {
                continue;
            }
            cmap.AddCidRangeMapping(startToken.Value.AsSpan(), endToken.Value.AsSpan(), (int)firstCidToken.Value);
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