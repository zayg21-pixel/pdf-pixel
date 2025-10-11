using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.Streams;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Cross-reference loader supporting classic tables and PDF 1.5+ cross-reference streams.
    /// Handles incremental updates by following the /Prev chain from the latest trailer backwards.
    /// Newest xref section is parsed first; older revisions never overwrite existing entries.
    /// </summary>
    internal sealed class PdfXrefLoader
    {
        private readonly PdfDocument _document;
        private readonly ILogger<PdfXrefLoader> _logger;
        private readonly PdfTrailerParser _trailerParser;

        public PdfXrefLoader(PdfDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _document = document;
            _logger = document.LoggerFactory.CreateLogger<PdfXrefLoader>();
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

            // Latest section first.
            if (context.MatchSequenceAt(xrefOffset, PdfTokens.Xref))
            {
                try
                {
                    context.Position = xrefOffset + PdfTokens.Xref.Length;
                    var trailer = ParseClassicXref(ref context);

                    // Walk /Prev chain backwards.
                    int? prevOffset;
                    while ((prevOffset = _trailerParser.GetPrevOffset(trailer)).HasValue)
                    {
                        int offsetValue = prevOffset.Value;
                        _logger.LogDebug("PdfXrefLoader: Following /Prev chain to offset {Offset} (classic path).", offsetValue);
                        if (context.MatchSequenceAt(offsetValue, PdfTokens.Xref))
                        {
                            context.Position = offsetValue + PdfTokens.Xref.Length;
                            trailer = ParseClassicXref(ref context);
                        }
                        else
                        {
                            context.Position = offsetValue;
                            trailer = ParseXrefStream(ref context);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PdfXrefLoader: Exception while parsing classic xref – continuing without index.");
                }
                return;
            }

            // XRef stream path.
            try
            {
                context.Position = xrefOffset;
                var trailer = ParseXrefStream(ref context);

                int? prevOffset;
                while ((prevOffset = _trailerParser.GetPrevOffset(trailer)).HasValue)
                {
                    int offsetValue = prevOffset.Value;
                    _logger.LogDebug("PdfXrefLoader: Following /Prev chain to offset {Offset} (stream path).", offsetValue);
                    if (context.MatchSequenceAt(offsetValue, PdfTokens.Xref))
                    {
                        context.Position = offsetValue + PdfTokens.Xref.Length;
                        trailer = ParseClassicXref(ref context);
                    }
                    else
                    {
                        context.Position = offsetValue;
                        trailer = ParseXrefStream(ref context);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PdfXrefLoader: Exception while parsing xref stream – continuing without index.");
            }
        }

        #region Classic XRef
        private PdfDictionary ParseClassicXref(ref PdfParseContext context)
        {
            int subsectionIndex = 0;
            while (!context.IsAtEnd)
            {
                int subsectionStartPos = context.Position;
                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (!PdfParsers.TryParseNumber(ref context, out int firstObject))
                {
                    _logger.LogDebug("PdfXrefLoader: Finished parsing classic xref (no more subsections). ");
                    break;
                }

                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                if (!PdfParsers.TryParseNumber(ref context, out int entryCount))
                {
                    _logger.LogWarning("PdfXrefLoader: Failed to read entry count for subsection {Index} (start {First}) at position {Pos}.", subsectionIndex, firstObject, context.Position);
                    context.Position = subsectionStartPos;
                    break;
                }

                PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                int parsedCount = 0;
                for (int localIndex = 0; localIndex < entryCount && !context.IsAtEnd; localIndex++)
                {
                    int loopPos = context.Position;
                    if (!ParseSingleEntry(ref context, firstObject + localIndex))
                    {
                        _logger.LogWarning("PdfXrefLoader: Failed xref entry index {LocalIndex} (object {ObjectNumber}) at position {Position}.", localIndex, firstObject + localIndex, loopPos);
                        break;
                    }
                    parsedCount++;
                }

                if (parsedCount != entryCount)
                {
                    _logger.LogWarning("PdfXrefLoader: Parsed {Parsed} of {Declared} entries in subsection {Index} (start {First}).", parsedCount, entryCount, subsectionIndex, firstObject);
                }

                if (_trailerParser.TryParseTrailerDictionary(ref context, out PdfDictionary trailer))
                {
                    TryApplyTrailer(trailer);
                    _logger.LogTrace("PdfXrefLoader: Encountered 'trailer' after subsection {Index}. Ending xref parse.");
                    return trailer;
                }

                subsectionIndex++;
            }

            return null;
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
                _logger.LogWarning("PdfXrefLoader: Unexpected end-of-buffer after generation for object {ObjectNumber}.");
                context.Position = entryStart;
                return false;
            }
            byte status = PdfParsingHelpers.PeekByte(ref context);
            context.Advance(1);
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

            var reference = new PdfReference(objectNumber, generation);
            PdfObjectInfo info;
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

            TryAddObjectIndexEntry(reference, info);
            return true;
        }
        #endregion

        #region XRef Stream (PDF 1.5+)
        private PdfDictionary ParseXrefStream(ref PdfParseContext context)
        {
            if (!PdfParsers.TryParseObjectHeader(ref context, out int objNum, out int gen))
            {
                _logger.LogDebug("PdfXrefLoader: startxref offset {Offset} does not begin with an indirect object header (not xref stream).", context.Position);
                return null;
            }
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            var value = PdfParsers.ParsePdfValue(ref context, _document, allowReferences: true);
            if (value == null)
            {
                _logger.LogDebug("PdfXrefLoader: Missing dictionary at xref stream object header.");
                return null;
            }
            var dictionary = value.AsDictionary();
            if (dictionary == null)
            {
                _logger.LogDebug("PdfXrefLoader: Xref stream object value not a dictionary.");
                return null;
            }
            string typeName = dictionary.GetName(PdfTokens.TypeKey);
            if (typeName != PdfTokens.XRefKey)
            {
                _logger.LogDebug("PdfXrefLoader: Object at offset {Offset} is not /Type /XRef (type={Type}).", context.Position, typeName);
                return null;
            }

            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
            if (!PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Stream))
            {
                _logger.LogWarning("PdfXrefLoader: XRef stream object missing 'stream' keyword.");
                return null;
            }

            var xrefObject = new PdfObject(new PdfReference(objNum, gen), _document, value);
            xrefObject.StreamData = PdfParsers.ParseStream(ref context, dictionary);
            var decoded = PdfStreamDecoder.DecodeContentStream(xrefObject);
            if (decoded.IsEmpty)
            {
                _logger.LogWarning("PdfXrefLoader: Decoded xref stream empty.");
                return null;
            }
            ParseXrefStreamEntries(dictionary, decoded);
            TryApplyTrailer(dictionary);
            return dictionary;
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
            var ranges = new List<(int start, int count)>();
            if (indexArray != null && indexArray.Count >= 2 && indexArray.Count % 2 == 0)
            {
                for (int rangeIndex = 0; rangeIndex < indexArray.Count; rangeIndex += 2)
                {
                    int start = indexArray.GetInteger(rangeIndex);
                    int count = indexArray.GetInteger(rangeIndex + 1);
                    if (count > 0)
                    {
                        ranges.Add((start, count));
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
                for (int localIndex = 0; localIndex < count; localIndex++)
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
                    int objNumber = start + localIndex;
                    var reference = new PdfReference(objNumber, type == 1 ? (int)field3 : (type == 0 ? (int)field3 : 0));
                    PdfObjectInfo info;
                    switch (type)
                    {
                        case 0:
                        {
                            info = PdfObjectInfo.ForFree(reference, (int)field2, (int)field3, true);
                            break;
                        }
                        case 1:
                        {
                            info = PdfObjectInfo.ForUncompressed(reference, field2, true);
                            break;
                        }
                        case 2:
                        {
                            if (field2 == 0)
                            {
                                continue;
                            }
                            info = PdfObjectInfo.ForCompressed(reference, (int)field2, (int)field3, true);
                            break;
                        }
                        default:
                        {
                            _logger.LogWarning("PdfXrefLoader: Unsupported xref stream entry type {Type} for object {Obj} (fields {F2},{F3}).", type, objNumber, field2, field3);
                            continue;
                        }
                    }

                    TryAddObjectIndexEntry(reference, info);
                }
            }
        }

        /// <summary>
        /// Add an entry to the document object index if not already present (newest wins).
        /// Logs a debug message when an older revision entry is skipped.
        /// </summary>
        /// <param name="reference">Object reference (number + generation).</param>
        /// <param name="info">Parsed xref information describing the object.</param>
        private void TryAddObjectIndexEntry(PdfReference reference, PdfObjectInfo info)
        {
            if (!_document.ObjectIndex.ContainsKey(reference))
            {
                _document.ObjectIndex[reference] = info;
                return;
            }

            _logger.LogDebug("PdfXrefLoader: Skipping older revision entry for object {Object} gen {Gen}.", reference.ObjectNumber, reference.Generation);
        }

        private static long ReadBigEndian(ReadOnlySpan<byte> slice)
        {
            long value = 0;
            for (int index = 0; index < slice.Length; index++)
            {
                value = (value << 8) | slice[index];
            }
            return value;
        }

        private void TryApplyTrailer(PdfDictionary dict)
        {
            if (dict == null)
            {
                return;
            }

            if (_document.RootObject == null)
            {
                _document.RootObject = dict.GetPageObject(PdfTokens.RootKey);
            }

            _trailerParser.TrySetDecryptor(dict);
        }
        #endregion

        #region Shared Helpers
        private static int LocateLastStartXref(ref PdfParseContext context)
        {
            ReadOnlySpan<byte> token = PdfTokens.Startxref;
            for (int scanIndex = context.Length - token.Length; scanIndex >= 0; scanIndex--)
            {
                if (context.MatchSequenceAt(scanIndex, token))
                {
                    return scanIndex;
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
            var temp = context;
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
