using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PdfReader.Models;

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

            // Classic table path.
            if (context.MatchSequenceAt(xrefOffset, PdfTokens.Xref))
            {
                try
                {
                    context.Position = xrefOffset + PdfTokens.Xref.Length;
                    var classicParser = new PdfParser(ref context, _document, allowReferences: true);
                    PdfDictionary trailer = ParseClassicXref(ref classicParser);
                    // Sync outer context after parsing.
                    context.Position = classicParser.Position;

                    // Walk /Prev chain backwards.
                    int? prevOffset;
                    while ((prevOffset = _trailerParser.GetPrevOffset(trailer)).HasValue)
                    {
                        int offsetValue = prevOffset.Value;
                        _logger.LogDebug("PdfXrefLoader: Following /Prev chain to offset {Offset} (classic path).", offsetValue);
                        if (context.MatchSequenceAt(offsetValue, PdfTokens.Xref))
                        {
                            context.Position = offsetValue + PdfTokens.Xref.Length;
                            classicParser = new PdfParser(ref context, _document, allowReferences: true);
                            trailer = ParseClassicXref(ref classicParser);
                            context.Position = classicParser.Position;
                        }
                        else
                        {
                            context.Position = offsetValue;
                            var streamParser = new PdfParser(ref context, _document, allowReferences: true);
                            trailer = ParseXrefStream(ref streamParser);
                            context.Position = streamParser.Position;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PdfXrefLoader: Exception while parsing classic xref – continuing without index.");
                }
                return;
            }

            // Stream path.
            try
            {
                context.Position = xrefOffset;
                var streamParserRoot = new PdfParser(ref context, _document, allowReferences: true);
                PdfDictionary streamTrailer = ParseXrefStream(ref streamParserRoot);
                context.Position = streamParserRoot.Position;

                int? prevOffset;
                while ((prevOffset = _trailerParser.GetPrevOffset(streamTrailer)).HasValue)
                {
                    int offsetValue = prevOffset.Value;
                    _logger.LogDebug("PdfXrefLoader: Following /Prev chain to offset {Offset} (stream path).", offsetValue);
                    if (context.MatchSequenceAt(offsetValue, PdfTokens.Xref))
                    {
                        context.Position = offsetValue + PdfTokens.Xref.Length;
                        var classicParser = new PdfParser(ref context, _document, allowReferences: true);
                        streamTrailer = ParseClassicXref(ref classicParser);
                        context.Position = classicParser.Position;
                    }
                    else
                    {
                        context.Position = offsetValue;
                        var streamParser = new PdfParser(ref context, _document, allowReferences: true);
                        streamTrailer = ParseXrefStream(ref streamParser);
                        context.Position = streamParser.Position;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PdfXrefLoader: Exception while parsing xref stream – continuing without index.");
            }
        }

        #region Classic XRef
        /// <summary>
        /// Parse classic xref subsections using PdfParser only. Format: (firstObject entryCount) lines followed by entries, ending with trailer operator.
        /// </summary>
        private PdfDictionary ParseClassicXref(ref PdfParser parser)
        {
            int subsectionIndex = 0;
            while (true)
            {
                int subsectionStart = parser.Position;
                IPdfValue firstValue = parser.ReadNextValue();
                if (firstValue == null)
                {
                    _logger.LogDebug("PdfXrefLoader: Finished parsing classic xref (EOF).");
                    break;
                }

                // Trailer detection.
                if (firstValue.Type == PdfValueType.Operator)
                {
                    PdfString op = firstValue.AsString();
                    if (!op.IsEmpty && op == PdfTokens.Trailer)
                    {
                        IPdfValue dictValue = parser.ReadNextValue();
                        PdfDictionary trailerDict = dictValue?.AsDictionary();
                        if (trailerDict != null)
                        {
                            TryApplyTrailer(trailerDict);
                        }
                        _logger.LogTrace("PdfXrefLoader: Encountered 'trailer' after subsection {Index}. Ending xref parse.", subsectionIndex);
                        return trailerDict;
                    }

                    // Unexpected operator -> treat as end.
                    _logger.LogDebug("PdfXrefLoader: Unexpected operator instead of subsection header; ending parse.");
                    break;
                }

                if (firstValue.Type != PdfValueType.Integer)
                {
                    // Not integer and not trailer -> end.
                    _logger.LogDebug("PdfXrefLoader: Non-integer subsection start value; ending classic xref parse.");
                    break;
                }

                IPdfValue countValue = parser.ReadNextValue();
                if (countValue == null || countValue.Type != PdfValueType.Integer)
                {
                    _logger.LogWarning("PdfXrefLoader: Failed to read entry count for subsection {Index} (start {First}) at position {Pos}.", subsectionIndex, firstValue.AsInteger(), parser.Position);
                    break;
                }

                int firstObject = firstValue.AsInteger();
                int entryCount = countValue.AsInteger();
                int parsedCount = 0;

                for (int localIndex = 0; localIndex < entryCount; localIndex++)
                {
                    int entryObjectNumber = firstObject + localIndex;
                    if (!ParseSingleEntry(ref parser, entryObjectNumber))
                    {
                        _logger.LogWarning("PdfXrefLoader: Failed xref entry index {LocalIndex} (object {ObjectNumber}) at position {Position}.", localIndex, entryObjectNumber, parser.Position);
                        break;
                    }
                    parsedCount++;
                }

                if (parsedCount != entryCount)
                {
                    _logger.LogWarning("PdfXrefLoader: Parsed {Parsed} of {Declared} entries in subsection {Index} (start {First}).", parsedCount, entryCount, subsectionIndex, firstObject);
                }

                subsectionIndex++;
            }

            return null;
        }

        /// <summary>
        /// Parse a single classic xref table entry using the unified PdfParser.
        /// Reads three tokens (offset, generation, status) without validating the first two types.
        /// Only the third (status) must be an operator 'n' or 'f'.
        /// </summary>
        private bool ParseSingleEntry(ref PdfParser parser, int objectNumber)
        {
            int entryStart = parser.Position;

            int offsetValue = parser.ReadNextValue().AsInteger();
            int generation = parser.ReadNextValue().AsInteger();
            PdfString statusString = parser.ReadNextValue().AsString();

            if (statusString.IsEmpty || statusString.Value.Length != 1)
            {
                parser.Position = entryStart;
                return false;
            }

            byte statusByte = statusString.Value.Span[0];

            var reference = new PdfReference(objectNumber, generation);
            PdfObjectInfo info;
            if (statusByte == (byte)'n')
            {
                info = PdfObjectInfo.ForUncompressed(reference, offsetValue, false);
            }
            else if (statusByte == (byte)'f')
            {
                info = PdfObjectInfo.ForFree(reference, 0, generation, false);
            }
            else
            {
                parser.Position = entryStart;
                return false;
            }

            TryAddObjectIndexEntry(reference, info);
            return true;
        }
        #endregion

        #region XRef Stream (PDF 1.5+)
        private PdfDictionary ParseXrefStream(ref PdfParser parser)
        {
            PdfObject xrefObject = parser.ReadObject();
            if (xrefObject == null || xrefObject.Dictionary == null)
            {
                _logger.LogDebug("PdfXrefLoader: startxref offset {Offset} did not yield a dictionary stream object.", parser.Position);
                return null;
            }

            if (xrefObject.StreamData.IsEmpty)
            {
                _logger.LogWarning("PdfXrefLoader: XRef stream object has no stream data.");
                return null;
            }

            var decoded = _document.StreamDecoder.DecodeContentStream(xrefObject);
            if (decoded.IsEmpty)
            {
                _logger.LogWarning("PdfXrefLoader: Decoded xref stream empty.");
                return null;
            }

            ParseXrefStreamEntries(xrefObject.Dictionary, decoded);
            TryApplyTrailer(xrefObject.Dictionary);
            return xrefObject.Dictionary;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            PdfParser parser = new PdfParser(ref temp, null, allowReferences: false);
            var value = parser.ReadNextValue();

            if (value.Type != PdfValueType.Integer)
            {
                return -1;
            }

            return value.AsInteger();
        }
        #endregion
    }
}
