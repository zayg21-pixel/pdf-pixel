using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.Streams;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Cross-reference loader supporting classic tables and (basic) PDF 1.5+ cross-reference streams (single revision, no /Prev chain yet).
    /// </summary>
    internal sealed class PdfXrefLoader
    {
        private readonly PdfDocument _document;
        private readonly ILogger<PdfXrefLoader> _logger;
        private readonly PdfTrailerParser _trailerParser;

        private const int MaxReasonableObjectNumber = 10_000_000; // Heuristic guardrail.
        private const int MaxReasonableEntryCount = 2_000_000;     // Prevent runaway allocations / loops.

        public PdfXrefLoader(PdfDocument document, ILogger<PdfXrefLoader> logger)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            _document = document;
            _logger = logger;
            _trailerParser = new PdfTrailerParser(document);
        }

        public void LoadXref(ref PdfParseContext context)
        {
            if (_document.FileBytes.IsEmpty)
            {
                _logger.LogWarning("PdfXrefLoader: File bytes empty – cannot load xref.");
                return;
            }

            context.Position = 0; // Start at beginning for backward scan.

            int startxrefPos = LocateLastStartXref(ref context);
            if (startxrefPos < 0)
            {
                _logger.LogWarning("PdfXrefLoader: 'startxref' keyword not found – falling back to legacy full scan.");
                return;
            }

            int xrefOffset = ParseStartXrefOffset(ref context, startxrefPos);
            if (xrefOffset < 0 || xrefOffset >= context.Length)
            {
                _logger.LogWarning("PdfXrefLoader: Parsed startxref offset {Offset} is invalid (file length {Length}).", xrefOffset, context.Length);
                return;
            }

            if (context.MatchSequenceAt(xrefOffset, PdfTokens.Xref))
            {
                // Classic xref
                try
                {
                    context.Position = xrefOffset + PdfTokens.Xref.Length;
                    ParseClassicXref(ref context);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PdfXrefLoader: Exception while parsing classic xref – continuing without index.");
                }
                return;
            }

            // Not classic; attempt xref stream (PDF 1.5+)
            try
            {
                if (!ParseXrefStream(ref context, xrefOffset))
                {
                    _logger.LogWarning("PdfXrefLoader: Offset {Offset} was not a classic table nor a valid xref stream.", xrefOffset);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PdfXrefLoader: Exception while parsing xref stream – continuing without index.");
            }
        }

        #region Classic XRef
        private void ParseClassicXref(ref PdfParseContext context)
        {
            int subsectionIndex = 0;
            while (!context.IsAtEnd)
            {
                int subsectionStartPos = context.Position;
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (!PdfParsers.TryParseNumber(ref context, out int firstObject))
                {
                    _logger.LogDebug("PdfXrefLoader: Finished parsing classic xref (no more subsections).");
                    break;
                }
                if (firstObject < 0 || firstObject > MaxReasonableObjectNumber)
                {
                    _logger.LogWarning("PdfXrefLoader: Subsection start object {First} out of range at position {Pos}.", firstObject, subsectionStartPos);
                    break;
                }
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (!PdfParsers.TryParseNumber(ref context, out int entryCount))
                {
                    _logger.LogWarning("PdfXrefLoader: Failed to read entry count for subsection {Index} (start {First}) at position {Pos}.", subsectionIndex, firstObject, context.Position);
                    context.Position = subsectionStartPos;
                    break;
                }
                if (entryCount < 0 || entryCount > MaxReasonableEntryCount)
                {
                    _logger.LogWarning("PdfXrefLoader: Unreasonable entry count {Count} for subsection {Index} (start {First}).", entryCount, subsectionIndex, firstObject);
                    break;
                }
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                int entryParsed = 0;
                for (int i = 0; i < entryCount; i++)
                {
                    int loopPos = context.Position;
                    if (!ParseSingleEntry(ref context, firstObject + i))
                    {
                        _logger.LogWarning("PdfXrefLoader: Failed xref entry index {LocalIndex} (object {ObjectNumber}) at position {Position}.", i, firstObject + i, loopPos);
                        break;
                    }
                    entryParsed++;
                }
                if (entryParsed != entryCount)
                {
                    _logger.LogWarning("PdfXrefLoader: Parsed {Parsed} of {Declared} entries in subsection {Index} (start {First}).", entryParsed, entryCount, subsectionIndex, firstObject);
                }

                if (_trailerParser.TryParseTrailerDictionary(ref context))
                {
                    _logger.LogDebug("PdfXrefLoader: Encountered 'trailer' after subsection {Index}. Ending xref parse.");
                    break;
                }

                subsectionIndex++;
            }
        }

        private bool ParseSingleEntry(ref PdfParseContext context, int objectNumber)
        {
            int entryStart = context.Position;
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            if (!PdfParsers.TryParseNumber(ref context, out int offsetValue))
            {
                _logger.LogWarning("PdfXrefLoader: Could not parse offset for object {ObjectNumber} at position {Pos}.", objectNumber, entryStart);
                context.Position = entryStart;
                return false;
            }
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            if (!PdfParsers.TryParseNumber(ref context, out int generation))
            {
                _logger.LogWarning("PdfXrefLoader: Could not parse generation for object {ObjectNumber} after offset at position {Pos}.", objectNumber, context.Position);
                context.Position = entryStart;
                return false;
            }
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            if (context.IsAtEnd)
            {
                _logger.LogWarning("PdfXrefLoader: Unexpected end-of-buffer after generation for object {ObjectNumber}.", objectNumber);
                context.Position = entryStart;
                return false;
            }
            byte status = PdfParsingHelpers.PeekByte(ref context);
            context.Advance(1);
            // Optional line ending
            if (!context.IsAtEnd && PdfParsingHelpers.PeekByte(ref context) == (byte)'\r')
            {
                context.Advance(1);
                if (!context.IsAtEnd && PdfParsingHelpers.PeekByte(ref context) == (byte)'\n')
                {
                    context.Advance(1);
                }
            }
            else if (!context.IsAtEnd && PdfParsingHelpers.PeekByte(ref context) == (byte)'\n')
            {
                context.Advance(1);
            }
            PdfReference reference = new PdfReference(objectNumber, generation);
            PdfObjectInfo info = null;
            if (status == (byte)'n')
            {
                info = PdfObjectInfo.ForUncompressed(reference, offsetValue, false);
            }
            else if (status == (byte)'f')
            {
                info = PdfObjectInfo.ForFree(reference, 0, generation, false);
            }
            else
            {
                return false;
            }
            _document.ObjectIndex[reference] = info;
            return true;
        }
        #endregion

        #region XRef Stream (PDF 1.5+)
        private bool ParseXrefStream(ref PdfParseContext rootContext, int xrefOffset)
        {
            // Create a temp context positioned at offset and parse object header.
            var temp = rootContext;
            temp.Position = xrefOffset;
            if (!PdfParsers.TryParseObjectHeader(ref temp, out int objNum, out int gen))
            {
                _logger.LogDebug("PdfXrefLoader: startxref offset {Offset} does not begin with an indirect object header (not xref stream).", xrefOffset);
                return false;
            }
            PdfParsingHelpers.SkipWhitespaceAndComment(ref temp);
            var value = PdfParsers.ParsePdfValue(ref temp, _document, allowReferences: true);
            if (value == null)
            {
                _logger.LogDebug("PdfXrefLoader: Missing dictionary at xref stream object header.");
                return false;
            }
            var dictionary = value.AsDictionary();
            if (dictionary == null)
            {
                _logger.LogDebug("PdfXrefLoader: Xref stream object value not a dictionary.");
                return false;
            }
            string typeName = dictionary.GetName(PdfTokens.TypeKey);
            if (!string.Equals(typeName, PdfTokens.XRefKey, System.StringComparison.Ordinal))
            {
                _logger.LogDebug("PdfXrefLoader: Object at offset {Offset} is not /Type /XRef (type={Type}).", xrefOffset, typeName);
                return false;
            }
            // Expect 'stream'
            PdfParsingHelpers.SkipWhitespaceAndComment(ref temp);
            if (!PdfParsingHelpers.MatchSequence(ref temp, PdfTokens.Stream))
            {
                _logger.LogWarning("PdfXrefLoader: XRef stream object missing 'stream' keyword.");
                return false;
            }
            var xrefObject = new PdfObject(new PdfReference(objNum, gen), _document, value);
            xrefObject.StreamData = PdfParsers.ParseStream(ref temp, dictionary);
            var decoded = PdfStreamDecoder.DecodeContentStream(xrefObject);
            if (decoded.IsEmpty)
            {
                _logger.LogWarning("PdfXrefLoader: Decoded xref stream empty.");
                return false;
            }
            ParseXrefStreamEntries(dictionary, decoded);
            ApplyTrailerFromDictionary(dictionary, isFromXrefStream: true);
            return true;
        }

        private void ParseXrefStreamEntries(PdfDictionary dict, ReadOnlyMemory<byte> decoded)
        {
            var wArray = dict.GetArray(PdfTokens.WKey);
            if (wArray == null || wArray.Count < 3)
            {
                _logger.LogWarning("PdfXrefLoader: XRef stream missing /W array.");
                return;
            }
            int w0 = wArray.GetInteger(0);
            int w1 = wArray.GetInteger(1);
            int w2 = wArray.GetInteger(2);
            if (w0 < 0 || w1 < 0 || w2 < 0)
            {
                _logger.LogWarning("PdfXrefLoader: Invalid negative /W widths.");
                return;
            }
            int entrySize = w0 + w1 + w2;
            if (entrySize <= 0)
            {
                _logger.LogWarning("PdfXrefLoader: Computed xref stream entry size is zero.");
                return;
            }
            var indexArray = dict.GetArray(PdfTokens.IndexKey);
            List<(int start, int count)> ranges = new List<(int start, int count)>();
            if (indexArray != null && indexArray.Count >= 2 && indexArray.Count % 2 == 0)
            {
                for (int i = 0; i < indexArray.Count; i += 2)
                {
                    int s = indexArray.GetInteger(i);
                    int c = indexArray.GetInteger(i + 1);
                    if (c > 0)
                    {
                        ranges.Add((s, c));
                    }
                }
            }
            else
            {
                int size = dict.GetIntegerOrDefault(PdfTokens.SizeKey);
                if (size > 0)
                {
                    ranges.Add((0, size));
                }
            }
            if (ranges.Count == 0)
            {
                _logger.LogWarning("PdfXrefLoader: No ranges to iterate in xref stream.");
                return;
            }
            var span = decoded.Span;
            int position = 0;
            foreach (var (start, count) in ranges)
            {
                for (int i = 0; i < count; i++)
                {
                    if (position + entrySize > span.Length)
                    {
                        _logger.LogWarning("PdfXrefLoader: Truncated xref stream (needed {Need} got {Rem}).", entrySize, span.Length - position);
                        return;
                    }
                    long type = w0 == 0 ? 1 : ReadBigEndian(span.Slice(position, w0));
                    position += w0;
                    long field2 = w1 == 0 ? 0 : ReadBigEndian(span.Slice(position, w1));
                    position += w1;
                    long field3 = w2 == 0 ? 0 : ReadBigEndian(span.Slice(position, w2));
                    position += w2;
                    int objNumber = start + i;
                    PdfReference reference = new PdfReference(objNumber, type == 1 ? (int)field3 : (type == 0 ? (int)field3 : 0));
                    PdfObjectInfo info = null;
                    switch (type)
                    {
                        case 0: // free
                            info = PdfObjectInfo.ForFree(reference, (int)field2, (int)field3, true);
                            break;
                        case 1: // uncompressed
                            info = PdfObjectInfo.ForUncompressed(reference, field2, true);
                            info.RawField2 = field2;
                            info.RawField3 = field3;
                            break;
                        case 2: // compressed
                            if (field2 == 0)
                            {
                                continue;
                            }
                            info = PdfObjectInfo.ForCompressed(reference, (int)field2, (int)field3, true);
                            info.RawField2 = field2;
                            info.RawField3 = field3;
                            break;
                        default:
                            _logger.LogWarning("PdfXrefLoader: Unsupported xref stream entry type {Type} for object {Obj} (fields {F2},{F3}).", type, objNumber, field2, field3);
                            continue;
                    }
                    _document.ObjectIndex[reference] = info;
                }
            }
        }

        private static long ReadBigEndian(ReadOnlySpan<byte> slice)
        {
            long value = 0;
            for (int i = 0; i < slice.Length; i++)
            {
                value = (value << 8) | slice[i];
            }
            return value;
        }

        private void ApplyTrailerFromDictionary(PdfDictionary dict, bool isFromXrefStream)
        {
            if (dict == null)
            {
                return;
            }
            if (_document.TrailerDictionary == null || isFromXrefStream)
            {
                _document.TrailerDictionary = dict;
            }

            _document.RootObject = dict.GetPageObject(PdfTokens.RootKey);
        }
        #endregion

        #region Shared Helpers
        private static int LocateLastStartXref(ref PdfParseContext context)
        {
            ReadOnlySpan<byte> token = PdfTokens.Startxref;
            for (int i = context.Length - token.Length; i >= 0; i--)
            {
                if (context.MatchSequenceAt(i, token))
                {
                    return i;
                }
            }
            return -1;
        }

        private static int ParseStartXrefOffset(ref PdfParseContext context, int startxrefPos)
        {
            int afterKeyword = startxrefPos + PdfTokens.Startxref.Length;
            if (afterKeyword >= context.Length)
            {
                return -1;
            }
            var temp = context; // copy
            temp.Position = afterKeyword;
            PdfParsingHelpers.SkipWhitespaceAndComment(ref temp);
            if (!PdfParsers.TryParseNumber(ref temp, out int offset))
            {
                return -1;
            }
            return offset;
        }
        #endregion
    }
}
