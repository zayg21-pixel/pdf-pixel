using System;
using System.Text;
using PdfReader.Parsing;
using PdfReader.Models;

namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// Parser for PDF ToUnicode CMaps used in CID fonts
    /// Refactored to use common PDF parsing patterns for better performance and consistency
    /// </summary>
    public static class PdfToUnicodeCMapParser
    {        
        public static PdfToUnicodeCMap ParseCMapFromContext(ref PdfParseContext context, PdfDocument document, PdfDictionary cmapDictionary)
        {
            var cmap = new PdfToUnicodeCMap();

            int? pendingCount = null;

            // Token-driven parsing: handle optional count before block keywords
            while (!context.IsAtEnd)
            {
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (context.IsAtEnd)
                {
                    break;
                }

                var token = PdfParsers.ParsePdfValue(ref context, document);
                if (token == null)
                {
                    context.Advance(1);
                    continue;
                }

                // Capture preceding numeric count
                if (token.Type == PdfValueType.Integer)
                {
                    pendingCount = token.AsInteger();
                    continue;
                }

                if (token.Type == PdfValueType.Operator)
                {
                    var op = token.AsString();
                    if (string.Equals(op, PdfTokens.BeginBfCharKey, StringComparison.Ordinal))
                    {
                        ParseBfCharMappings(ref context, cmap, document, pendingCount);
                        pendingCount = null;
                        continue;
                    }
                    if (string.Equals(op, PdfTokens.BeginBfRangeKey, StringComparison.Ordinal))
                    {
                        ParseBfRangeMappings(ref context, cmap, document, pendingCount);
                        pendingCount = null;
                        continue;
                    }
                    if (string.Equals(op, PdfTokens.BeginCodespaceRangeKey, StringComparison.Ordinal))
                    {
                        ParseCodespaceRangesInternal(ref context, cmap, document, pendingCount);
                        pendingCount = null;
                        continue;
                    }
                    if (string.Equals(op, PdfTokens.BeginCMapKey, StringComparison.Ordinal))
                    {
                        // No-op; body handled by inner ops
                        continue;
                    }
                    if (string.Equals(op, PdfTokens.EndCMapKey, StringComparison.Ordinal))
                    {
                        break;
                    }
                    if (string.Equals(op, PdfTokens.UseCMapKey, StringComparison.Ordinal))
                    {
                        // Resolve base CMap by name; consult document cache first
                        ResolveAndMergeUseCMap(ref context, document, cmap);
                        continue;
                    }
                    if (string.Equals(op, PdfTokens.EndBfCharKey, StringComparison.Ordinal) ||
                        string.Equals(op, PdfTokens.EndBfRangeKey, StringComparison.Ordinal) ||
                        string.Equals(op, PdfTokens.EndCodespaceRangeKey, StringComparison.Ordinal))
                    {
                        // If encountered outside expected place, just continue
                        continue;
                    }
                }

                // Unknown token/content: already consumed; proceed
            }
            
            // After parsing, attempt to cache the CMap by name for usecmap resolution.
            string cmapName = null;
            if (cmapDictionary != null)
            {
                cmapName = cmapDictionary.GetName(PdfTokens.CMapNameKey);
                if (string.IsNullOrEmpty(cmapName))
                {
                    cmapName = cmapDictionary.GetName(PdfTokens.NameKey);
                }
            }
            // Only cache if a name is found and the CMap is valid.
            if (!string.IsNullOrEmpty(cmapName) && cmap.MappingCount > 0)
            {
                document.ToUnicodeCmaps[cmapName] = cmap;
            }
            return cmap.MappingCount > 0 ? cmap : null;
        }
        
        private static void ResolveAndMergeUseCMap(ref PdfParseContext context, PdfDocument document, PdfToUnicodeCMap target)
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
            if (!string.IsNullOrEmpty(cmapName) && document.ToUnicodeCmaps.TryGetValue(cmapName, out var cached))
            {
                target.MergeFrom(cached, overwriteExisting: false);
                return;
            }
        }

        private static void ParseBfCharMappings(ref PdfParseContext context, PdfToUnicodeCMap cmap, PdfDocument document, int? expectedCount)
        {
            int parsed = 0;
            while (!context.IsAtEnd)
            {
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (context.IsAtEnd)
                {
                    break;
                }

                if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.EndBfChar))
                {
                    break;
                }

                if (expectedCount.HasValue && parsed >= expectedCount.Value)
                {
                    var maybeEnd = PdfParsers.ParsePdfValue(ref context, document);
                    if (maybeEnd?.Type == PdfValueType.Operator && string.Equals(maybeEnd.AsString(), PdfTokens.EndBfCharKey, StringComparison.Ordinal))
                    {
                        break;
                    }
                    continue;
                }
                
                var srcValue = PdfParsers.ParsePdfValue(ref context, document);
                if (srcValue?.Type != PdfValueType.HexString)
                {
                    context.Advance(1);
                    continue;
                }
                
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                var dstValue = PdfParsers.ParsePdfValue(ref context, document);
                if (dstValue == null)
                {
                    continue;
                }
                
                try
                {
                    var srcBytes = srcValue.AsHexBytes();
                    if (srcBytes == null || srcBytes.Length == 0)
                    {
                        continue;
                    }

                    var code = new PdfCharacterCode(srcBytes);

                    string unicode = null;
                    if (dstValue.Type == PdfValueType.HexString)
                    {
                        var uniBytes = dstValue.AsHexBytes();
                        if (uniBytes == null || uniBytes.Length == 0)
                        {
                            continue;
                        }
                        if (IsSentinelFFFF(uniBytes))
                        {
                            parsed++;
                            continue;
                        }
                        unicode = ParseBytesToUnicode(uniBytes);
                    }
                    else if (dstValue.Type == PdfValueType.String)
                    {
                        unicode = dstValue.AsString();
                    }

                    if (!string.IsNullOrEmpty(unicode))
                    {
                        cmap.AddMapping(code, unicode);
                    }
                    parsed++;
                }
                catch
                {
                    continue;
                }
            }
        }
        
        private static void ParseBfRangeMappings(ref PdfParseContext context, PdfToUnicodeCMap cmap, PdfDocument document, int? expectedCount)
        {
            int parsed = 0;
            while (!context.IsAtEnd)
            {
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (context.IsAtEnd)
                {
                    break;
                }
                
                if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.EndBfRange))
                {
                    break;
                }

                if (expectedCount.HasValue && parsed >= expectedCount.Value)
                {
                    var maybeEnd = PdfParsers.ParsePdfValue(ref context, document);
                    if (maybeEnd?.Type == PdfValueType.Operator && string.Equals(maybeEnd.AsString(), PdfTokens.EndBfRangeKey, StringComparison.Ordinal))
                    {
                        break;
                    }
                    continue;
                }

                var startVal = PdfParsers.ParsePdfValue(ref context, document);
                if (startVal?.Type != PdfValueType.HexString)
                {
                    context.Advance(1);
                    continue;
                }

                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                var endVal = PdfParsers.ParsePdfValue(ref context, document);
                if (endVal?.Type != PdfValueType.HexString)
                {
                    continue;
                }

                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                var third = PdfParsers.ParsePdfValue(ref context, document);
                if (third == null)
                {
                    continue;
                }

                try
                {
                    var startBytes = startVal.AsHexBytes();
                    var endBytes = endVal.AsHexBytes();
                    if (startBytes == null || endBytes == null || startBytes.Length != endBytes.Length)
                    {
                        continue;
                    }

                    // Case 1: sequential range mapping via starting Unicode string
                    if (third.Type == PdfValueType.HexString)
                    {
                        var bytes = third.AsHexBytes();
                        if (bytes == null || bytes.Length == 0 || IsSentinelFFFF(bytes))
                        {
                            continue;
                        }
                        int startCodePoint = FirstCodePointFromUtf16BE(bytes);
                        cmap.AddRangeMapping(startBytes, endBytes, startCodePoint);
                        parsed++;
                    }
                    // Case 2: explicit array of destination strings for each source code
                    else if (third.Type == PdfValueType.Array)
                    {
                        var arr = third.AsArray();
                        if (arr == null || arr.Count == 0)
                        {
                            continue;
                        }

                        // iterate codes and array values in lockstep
                        int len = startBytes.Length;
                        uint vStart = BytesToUIntBE(startBytes);
                        uint vEnd = BytesToUIntBE(endBytes);
                        uint v = vStart;

                        for (int i = 0; i < arr.Count && v <= vEnd; i++, v++)
                        {
                            var vItem = arr.GetValue(i);
                            string unicode = null;

                            if (vItem?.Type == PdfValueType.HexString)
                            {
                                var hex = vItem.AsHexBytes();
                                if (hex == null || hex.Length == 0 || IsSentinelFFFF(hex))
                                {
                                    continue;
                                }
                                unicode = ParseBytesToUnicode(hex);
                            }
                            else if (vItem?.Type == PdfValueType.String)
                            {
                                unicode = vItem.AsString();
                            }

                            if (!string.IsNullOrEmpty(unicode))
                            {
                                var codeBytes = UIntToBytesBE(v, len);
                                cmap.AddMapping(new PdfCharacterCode(codeBytes), unicode);
                            }
                        }
                        parsed++;
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        private static void ParseCodespaceRangesInternal(ref PdfParseContext context, PdfToUnicodeCMap cmap, PdfDocument document, int? expectedCount)
        {
            int parsed = 0;
            while (!context.IsAtEnd)
            {
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.EndCodespaceRange))
                {
                    break;
                }

                if (expectedCount.HasValue && parsed >= expectedCount.Value)
                {
                    var maybeEnd = PdfParsers.ParsePdfValue(ref context, document);
                    if (maybeEnd?.Type == PdfValueType.Operator && string.Equals(maybeEnd.AsString(), PdfTokens.EndCodespaceRangeKey, StringComparison.Ordinal))
                    {
                        break;
                    }
                    continue;
                }

                // Expect pairs of hex strings: <start> <end>
                var startVal = PdfParsers.ParsePdfValue(ref context, document);
                if (startVal?.Type != PdfValueType.HexString)
                {
                    context.Advance(1);
                    continue;
                }
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                var endVal = PdfParsers.ParsePdfValue(ref context, document);
                if (endVal?.Type != PdfValueType.HexString)
                {
                    continue;
                }

                var startBytes = startVal.AsHexBytes();
                var endBytes = endVal.AsHexBytes();
                if (startBytes != null && endBytes != null && startBytes.Length == endBytes.Length && startBytes.Length > 0 && startBytes.Length <= 4)
                {
                    cmap.AddCodespaceRange(startBytes, endBytes);
                    parsed++;
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