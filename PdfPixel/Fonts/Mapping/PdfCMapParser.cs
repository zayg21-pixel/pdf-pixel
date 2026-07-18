using System;
using System.Text;
using PdfPixel.Models;
using PdfPixel.Text;
using PdfPixel.PostScript;
using Microsoft.Extensions.Logging;
using PdfPixel.PostScript.Tokens;
using System.Linq;
using PdfPixel.Fonts.Model;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Parser for PDF CMaps used in CID fonts.
/// </summary>
public static class PdfCMapParser
{
    /// <summary>
    /// Parses a PDF CMap stream into a <see cref="PdfCMap"/> containing all code-to-Unicode
    /// and code-to-CID mappings declared in the stream, including those inherited via
    /// <c>usecmap</c>.
    /// </summary>
    /// <param name="cmapBytes">Raw bytes of the CMap stream.</param>
    /// <param name="loggerFactory">Logger factory passed to the PostScript evaluator.</param>
    /// <param name="cmapProvider">
    /// Callback that resolves a named CMap referenced by a <c>usecmap</c> entry;
    /// may return <see langword="null"/> if the named CMap is unavailable.
    /// </param>
    /// <returns>
    /// The populated <see cref="PdfCMap"/>, or an empty instance when the stream
    /// does not produce a recognisable CMap dictionary.
    /// </returns>
    public static PdfCMap? ParseCMap(in ReadOnlyMemory<byte> cmapBytes, ILoggerFactory loggerFactory, Func<PdfString, PdfCMap?> cmapProvider)
    {
        PdfCMap cmap = new();
        PostScriptEvaluator evaluator = new(cmapBytes.Span, false, loggerFactory.CreateLogger<PostScriptEvaluator>());

        evaluator.SetResourceValue("ProcSet", "CIDInit", new PostScriptDictionary());
        System.Collections.Generic.Stack<PostScriptToken> stack = [];
        evaluator.EvaluateTokens(stack);

        PostScriptDictionary? cmaps = evaluator.GetResourceCategory(PdfTokens.CMapKey.ToString());
        if (cmaps?.Entries.FirstOrDefault().Value is PostScriptDictionary cmapDictionary)
        {
            cmap.CidSystemInfo = GetInfo(cmapDictionary);

            if (cmapDictionary.Entries.TryGetValue("codespacerange", out PostScriptToken? codespaceRange) && codespaceRange is PostScriptArray codespaceRangeArray)
            {
                PostScriptToken[] arrayItems = codespaceRangeArray.Elements;

                foreach (PostScriptToken item in arrayItems)
                {
                    if (item is PostScriptArray innerArray)
                    {
                        ParseCodespaceRangeMapping(innerArray.Elements, cmap);
                    }
                }
            }

            if (cmapDictionary.Entries.TryGetValue("bfchar", out PostScriptToken? bfChar) && bfChar is PostScriptArray bfCharArray)
            {
                PostScriptToken[] arrayItems = bfCharArray.Elements;

                foreach (PostScriptToken item in arrayItems)
                {
                    if (item is PostScriptArray innerArray)
                    {
                        ParseBfCharMappings(innerArray.Elements, cmap);
                    }
                }
            }

            if (cmapDictionary.Entries.TryGetValue("bfrange", out PostScriptToken? bfRange) && bfRange is PostScriptArray bfRangeArray)
            {
                PostScriptToken[] arrayItems = bfRangeArray.Elements;

                foreach (PostScriptToken item in arrayItems)
                {
                    if (item is PostScriptArray innerArray)
                    {
                        ParseBfRangeMappings(innerArray.Elements, cmap);
                    }
                }

            }

            if (cmapDictionary.Entries.TryGetValue("cidchar", out PostScriptToken? cidChar) && cidChar is PostScriptArray cidCharArray)
            {
                PostScriptToken[] arrayItems = cidCharArray.Elements;

                foreach (PostScriptToken item in arrayItems)
                {
                    if (item is PostScriptArray innerArray)
                    {
                        ParseCidCharMappings(innerArray.Elements, cmap);
                    }
                }
            }

            if (cmapDictionary.Entries.TryGetValue("cidrange", out PostScriptToken? cidRange) && cidRange is PostScriptArray cidRangeArray)
            {
                PostScriptToken[] arrayItems = cidRangeArray.Elements;

                foreach (PostScriptToken item in arrayItems)
                {
                    if (item is PostScriptArray innerArray)
                    {
                        ParseCidRangeMappings(innerArray.Elements, cmap);
                    }
                }
            }

            if (cmapDictionary.Entries.TryGetValue("usecmap", out PostScriptToken? useCMapToken) && useCMapToken is PostScriptArray useCMapArray)
            {
                foreach (PostScriptToken element in useCMapArray.Elements)
                {
                    if (element is PostScriptLiteralName useCMapName)
                    {
                        PdfCMap? baseCMap = cmapProvider?.Invoke(PdfString.FromString(useCMapName.Name));
                        if (baseCMap != null)
                        {
                            cmap.MergeFrom(baseCMap);
                        }
                    }
                }
            }

            if (cmapDictionary.Entries.TryGetValue("WMode", out PostScriptToken? wmodeToken) && wmodeToken is PostScriptNumber wmodeNumber)
            {
                cmap.WMode = (CMapWMode)(int)wmodeNumber.Number;
            }

            if (cmapDictionary.Entries.TryGetValue("CMapName", out PostScriptToken? name) && name is PostScriptLiteralName nameLiteral)
            {
                cmap.Name = PdfString.FromString(nameLiteral.Name);
            }
        }

        if (cmap.IsEmpty)
        {
            PdfCMap fallback = PdfCMapScanner.Scan(cmapBytes);
            if (!fallback.IsEmpty)
            {
                loggerFactory.CreateLogger<PdfCMap>()
                    .LogWarning("CMap PostScript evaluation produced no usable ranges; recovered mappings by scanning the raw CMap stream instead.");
                return fallback;
            }
        }

        return cmap;
    }

    internal static PdfCMap? ParseCMap(in ReadOnlyMemory<byte> cmapBytes, IPdfDocumentInternal document)
    {
        return ParseCMap(
            cmapBytes,
            document.LoggerFactory,
            document.CMapCache.GetCmap
        );
    }

    private static PdfCidSystemInfo? GetInfo(PostScriptDictionary dictionary)
    {
        if (dictionary == null)
        {
            return null;
        }

        if (dictionary.Entries.TryGetValue(PdfTokens.CidSystemInfoKey.ToString(), out PostScriptToken? infoValue) && infoValue is PostScriptDictionary infoDictionary)
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

            cmap.AddCodespaceRange(startToken.Data.AsSpan(), endToken.Data.AsSpan());
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

            PdfCharacterCode code = new(codeString.Data);

            Span<byte> unicodeBytes = unicodeString.Data.AsSpan();

            if (!IsSentinelFFFF(unicodeBytes) && !IsSentinelFFFD(unicodeBytes))
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
            PostScriptToken thirdToken = tokens[i++];

            if (startToken == null || endToken == null || thirdToken == null)
            {
                continue;
            }

            Span<byte> startBytes = startToken.Data.AsSpan();
            Span<byte> endBytes = endToken.Data.AsSpan();

            if (thirdToken is PostScriptString unicodeString)
            {
                ReadOnlySpan<byte> unicodeBytes = unicodeString.Data.AsSpan();
                if (IsSentinelFFFF(unicodeBytes) || IsSentinelFFFD(unicodeBytes))
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
                    cmap.AddMapping(new PdfCharacterCode(startToken.Data), startUnicodeFull);
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

                    if (PdfCMap.IsValidCodePoint(scalar))
                    {
                        string unicode = char.ConvertFromUtf32(scalar);
                        ReadOnlyMemory<byte> packed = PdfCharacterCode.PackUIntToBigEndian(current, codeLength);
                        cmap.AddMapping(new PdfCharacterCode(packed), unicode);
                    }
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
                    if (!(array.Elements[arrayIndex] is PostScriptString arrayItem))
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> hex = arrayItem.Data.AsSpan();
                    if (IsSentinelFFFF(hex) || IsSentinelFFFD(hex))
                    {
                        continue;
                    }

                    string unicode = ParseBytesToUnicode(hex);
                    ReadOnlyMemory<byte> codeBytes = PdfCharacterCode.PackUIntToBigEndian(codeCurrent, codeLength);
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

            cmap.AddCidMapping(new PdfCharacterCode(codeToken.Data), (int)cidToken.Number);
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

            cmap.AddCidRangeMapping(startToken.Data.AsSpan(), endToken.Data.AsSpan(), (int)firstCidToken.Number);
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
    private static string ParseBytesToUnicode(in ReadOnlySpan<byte> hex)
    {
        ReadOnlySpan<byte> localHex = hex;
        SliceBom(ref localHex);
        return Encoding.BigEndianUnicode.GetString(localHex);
    }

    private static bool IsSentinelFFFF(in ReadOnlySpan<byte> bytes) => bytes.Length == 2 && bytes[0] == 0xFF && bytes[1] == 0xFF;

    // Adobe's CID-to-Unicode source data uses <FFFD> as a "no Unicode value" sentinel for a CID.
    private static bool IsSentinelFFFD(in ReadOnlySpan<byte> bytes) => bytes.Length == 2 && bytes[0] == 0xFF && bytes[1] == 0xFD;
}
