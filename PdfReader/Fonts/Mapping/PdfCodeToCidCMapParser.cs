using System;
using PdfReader.Models;
using PdfReader.Parsing;

namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// Parser for code-to-CID CMaps (Type0 /Encoding CMaps).
    /// Supports: begincodespacerange, begincidchar, begincidrange, usecmap (no-op for now).
    /// </summary>
    public static class PdfCodeToCidCMapParser
    {
        public static PdfCodeToCidCMap ParseCMapFromContext(ref PdfParseContext context, PdfDocument document, PdfDictionary cmapDictionary)
        {
            var cmap = new PdfCodeToCidCMap();
            int? pendingCount = null;

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

                if (token.Type == PdfValueType.Integer)
                {
                    pendingCount = token.AsInteger();
                    continue;
                }

                if (token.Type == PdfValueType.Operator)
                {
                    var op = token.AsString();

                    if (string.Equals(op, PdfTokens.BeginCodespaceRangeKey, StringComparison.Ordinal))
                    {
                        ParseCodespaceRanges(ref context, cmap, document, pendingCount);
                        pendingCount = null;
                        continue;
                    }

                    if (string.Equals(op, PdfTokens.BeginCidCharKey, StringComparison.Ordinal))
                    {
                        ParseCidChar(ref context, cmap, document, pendingCount);
                        pendingCount = null;
                        continue;
                    }

                    if (string.Equals(op, PdfTokens.BeginCidRangeKey, StringComparison.Ordinal))
                    {
                        ParseCidRange(ref context, cmap, document, pendingCount);
                        pendingCount = null;
                        continue;
                    }

                    if (string.Equals(op, PdfTokens.UseCMapKey, StringComparison.Ordinal))
                    {
                        // TODO: resolve base code-to-CID CMap (separate cache from ToUnicode)
                        // See ResolveAndMergeUseCMap implementation below.
                        ResolveAndMergeUseCMap(ref context, document, cmap);
                        continue;
                    }

                    if (string.Equals(op, PdfTokens.EndCodespaceRangeKey, StringComparison.Ordinal) ||
                        string.Equals(op, PdfTokens.EndCidCharKey, StringComparison.Ordinal) ||
                        string.Equals(op, PdfTokens.EndCidRangeKey, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
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
            if (!string.IsNullOrEmpty(cmapName) && (cmap.MappingCount > 0 || cmap.HasCodeSpaceRanges))
            {
                document.CodeToCidCMaps[cmapName] = cmap;
            }
            if (cmap.MappingCount > 0 || cmap.HasCodeSpaceRanges)
            {
                return cmap;
            }

            return null;
        }

        /// <summary>
        /// Resolves and merges a base code-to-CID CMap referenced by usecmap.
        /// </summary>
        /// <param name="context">PDF parse context.</param>
        /// <param name="document">PDF document.</param>
        /// <param name="target">Target CMap to merge into.</param>
        private static void ResolveAndMergeUseCMap(ref PdfParseContext context, PdfDocument document, PdfCodeToCidCMap target)
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

            if (!string.IsNullOrEmpty(cmapName) && document.CodeToCidCMaps.TryGetValue(cmapName, out var baseCMap))
            {
                if (baseCMap != null)
                {
                    target.MergeFrom(baseCMap);
                }
            }
        }

        private static void ParseCodespaceRanges(ref PdfParseContext context, PdfCodeToCidCMap cmap, PdfDocument document, int? expectedCount)
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
                if (startBytes != null && endBytes != null)
                {
                    cmap.AddCodespaceRange(startBytes, endBytes);
                    parsed++;
                }
            }
        }

        private static void ParseCidChar(ref PdfParseContext context, PdfCodeToCidCMap cmap, PdfDocument document, int? expectedCount)
        {
            int parsed = 0;
            while (!context.IsAtEnd)
            {
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.EndCidChar))
                {
                    break;
                }

                if (expectedCount.HasValue && parsed >= expectedCount.Value)
                {
                    var maybeEnd = PdfParsers.ParsePdfValue(ref context, document);
                    if (maybeEnd?.Type == PdfValueType.Operator && string.Equals(maybeEnd.AsString(), PdfTokens.EndCidCharKey, StringComparison.Ordinal))
                    {
                        break;
                    }
                    continue;
                }

                var src = PdfParsers.ParsePdfValue(ref context, document);
                if (src?.Type != PdfValueType.HexString)
                {
                    context.Advance(1);
                    continue;
                }

                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                var dst = PdfParsers.ParsePdfValue(ref context, document);
                if (dst?.Type != PdfValueType.Integer)
                {
                    continue;
                }

                var bytes = src.AsHexBytes();
                if (bytes == null || bytes.Length == 0)
                {
                    continue;
                }

                cmap.AddMapping(new PdfCharacterCode(bytes), dst.AsInteger());
                parsed++;
            }
        }

        private static void ParseCidRange(ref PdfParseContext context, PdfCodeToCidCMap cmap, PdfDocument document, int? expectedCount)
        {
            int parsed = 0;
            while (!context.IsAtEnd)
            {
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.EndCidRange))
                {
                    break;
                }

                if (expectedCount.HasValue && parsed >= expectedCount.Value)
                {
                    var maybeEnd = PdfParsers.ParsePdfValue(ref context, document);
                    if (maybeEnd?.Type == PdfValueType.Operator && string.Equals(maybeEnd.AsString(), PdfTokens.EndCidRangeKey, StringComparison.Ordinal))
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
                var firstCidVal = PdfParsers.ParsePdfValue(ref context, document);
                if (firstCidVal?.Type != PdfValueType.Integer)
                {
                    continue;
                }

                var startBytes = startVal.AsHexBytes();
                var endBytes = endVal.AsHexBytes();
                if (startBytes == null || endBytes == null)
                {
                    continue;
                }

                cmap.AddRangeMapping(startBytes, endBytes, firstCidVal.AsInteger());
                parsed++;
            }
        }
    }
}
