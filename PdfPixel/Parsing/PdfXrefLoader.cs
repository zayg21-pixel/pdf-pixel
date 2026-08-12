using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

/// <summary>
/// Cross-reference loader supporting classic tables and PDF 1.5+ cross-reference streams.
/// Handles incremental updates by following the /Prev chain from the latest trailer backwards.
/// Newest xref section is parsed first; older revisions never overwrite existing entries.
/// </summary>
internal sealed class PdfXrefLoader
{
    private readonly IPdfDocumentInternal _document;
    private readonly ILogger<PdfXrefLoader> _logger;
    private readonly PdfTrailerParser _trailerParser;

    public PdfXrefLoader(IPdfDocumentInternal document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        _document = document;
        _logger = document.LoggerFactory.CreateLogger<PdfXrefLoader>();
        _trailerParser = new PdfTrailerParser(document);
    }

    /// <summary>
    /// Loads the cross-reference table/stream(s) into the document's object index.
    /// </summary>
    public void LoadXref()
    {
        long startxrefPos = LocateLastStartXref();
        if (startxrefPos < 0)
        {
            _logger.LogError("'startxref' keyword not found.");
            return;
        }

        int declaredOffset = ParseStartXrefOffset(startxrefPos);
        int xrefOffset = _document.HeaderOffset + declaredOffset;
        if (declaredOffset < 0 || xrefOffset >= _document.Stream.Length)
        {
            _logger.LogWarning("Parsed startxref offset {Offset} is invalid (file length {Length}).", declaredOffset, _document.Stream.Length);
            return;
        }

        PdfParser parser = new(_document.Stream, _document, allowReferences: true, decrypt: false);
        HashSet<int> visitedOffsets = [xrefOffset];

        // Classic table path.
        if (MatchSequenceAt(xrefOffset, PdfTokens.Xref))
        {
            try
            {
                parser.Position = xrefOffset + PdfTokens.Xref.Length;
                PdfDictionary? trailer = ParseClassicXref(ref parser);
                ApplyHybridCrossReferenceStream(trailer);

                // Walk /Prev chain backwards.
                int? prevOffset;
                while ((prevOffset = _trailerParser.GetPrevOffset(trailer)).HasValue)
                {
                    int offsetValue = _document.HeaderOffset + prevOffset.Value;
                    if (!visitedOffsets.Add(offsetValue))
                    {
                        _logger.LogWarning("Detected /Prev chain cycle at offset {Offset}; stopping xref traversal.", offsetValue);
                        break;
                    }

                    _logger.LogDebug("Following /Prev chain to offset {Offset} (classic path).", offsetValue);
                    if (MatchSequenceAt(offsetValue, PdfTokens.Xref))
                    {
                        parser.Position = offsetValue + PdfTokens.Xref.Length;
                        trailer = ParseClassicXref(ref parser);
                        ApplyHybridCrossReferenceStream(trailer);
                    }
                    else
                    {
                        PdfParser streamParser = new(_document.Stream, _document, allowReferences: true, decrypt: false);
                        streamParser.Position = offsetValue;
                        trailer = ParseXrefStream(ref streamParser);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception while parsing classic xref.");
                throw new PdfInvalidDocumentException("Failed to parse classic xref.", ex);
            }

            return;
        }

        // Stream path.
        try
        {
            parser.Position = xrefOffset;
            PdfDictionary? streamTrailer = ParseXrefStream(ref parser);

            int? prevOffset;
            while ((prevOffset = _trailerParser.GetPrevOffset(streamTrailer)).HasValue)
            {
                int offsetValue = _document.HeaderOffset + prevOffset.Value;
                if (!visitedOffsets.Add(offsetValue))
                {
                    _logger.LogWarning("Detected /Prev chain cycle at offset {Offset}; stopping xref traversal.", offsetValue);
                    break;
                }

                _logger.LogDebug("Following /Prev chain to offset {Offset} (stream path).", offsetValue);
                if (MatchSequenceAt(offsetValue, PdfTokens.Xref))
                {
                    parser.Position = offsetValue + PdfTokens.Xref.Length;
                    streamTrailer = ParseClassicXref(ref parser);
                    ApplyHybridCrossReferenceStream(streamTrailer);
                }
                else
                {
                    parser.Position = offsetValue;
                    streamTrailer = ParseXrefStream(ref parser);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while parsing xref stream.");
            throw new PdfInvalidDocumentException("Failed to parse xref stream.", ex);
        }
    }

    #region Classic XRef

    /// <summary>
    /// Parse classic xref subsections using PdfParser only. Format: (firstObject entryCount) lines followed by entries, ending with trailer operator.
    /// </summary>
    private PdfDictionary? ParseClassicXref(ref PdfParser parser)
    {
        int subsectionIndex = 0;
        while (true)
        {
            IPdfValue? firstValue = parser.ReadNextValue();
            if (firstValue == null)
            {
                _logger.LogDebug("Finished parsing classic xref (EOF).");
                break;
            }

            // Trailer detection.
            if (firstValue.Type == PdfValueType.Operator)
            {
                PdfString op = firstValue.AsString();
                if (!op.IsEmpty && op == PdfTokens.Trailer)
                {
                    IPdfValue? dictValue = parser.ReadNextValue();
                    PdfDictionary? trailerDict = dictValue?.AsDictionary();
                    if (trailerDict != null)
                    {
                        TryApplyTrailer(trailerDict);
                    }

                    _logger.LogTrace("Encountered 'trailer' after subsection {Index}. Ending xref parse.", subsectionIndex);
                    return trailerDict;
                }

                // Unexpected operator -> treat as end.
                _logger.LogDebug("Unexpected operator instead of subsection header; ending parse.");
                break;
            }

            if (firstValue.Type != PdfValueType.Integer)
            {
                // Not integer and not trailer -> end.
                _logger.LogDebug("Non-integer subsection start value; ending classic xref parse.");
                break;
            }

            IPdfValue? countValue = parser.ReadNextValue();
            if (countValue == null || countValue.Type != PdfValueType.Integer)
            {
                _logger.LogWarning("Failed to read entry count for subsection {Index} (start {First}) at position {Pos}.", subsectionIndex, firstValue.AsInteger(), parser.Position);
                break;
            }

            int firstObject = firstValue.AsInteger();
            int entryCount = countValue.AsInteger();
            int parsedCount = 0;

            for (int localIndex = 0; localIndex < entryCount; localIndex++)
            {
                var entryObjectNumber = (uint)(firstObject + localIndex);
                if (!ParseSingleEntry(ref parser, entryObjectNumber))
                {
                    _logger.LogWarning("Failed xref entry index {LocalIndex} (object {ObjectNumber}) at position {Position}.", localIndex, entryObjectNumber, parser.Position);
                    break;
                }

                parsedCount++;
            }

            if (parsedCount != entryCount)
            {
                _logger.LogWarning("Parsed {Parsed} of {Declared} entries in subsection {Index} (start {First}).", parsedCount, entryCount, subsectionIndex, firstObject);
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
    private bool ParseSingleEntry(ref PdfParser parser, uint objectNumber)
    {
        int entryStart = parser.Position;

        var offsetValue = (uint)parser.ReadNextValue().AsInteger();
        int generation = parser.ReadNextValue().AsInteger();
        PdfString statusString = parser.ReadNextValue().AsString();

        if (statusString.IsEmpty || statusString.Value.Length != 1)
        {
            parser.Position = entryStart;
            return false;
        }

        byte statusByte = statusString.Value.Span[0];

        PdfReference reference = new(objectNumber, generation);
        PdfObjectInfo info;
        if (statusByte == (byte)'n')
        {
            info = PdfObjectInfo.ForUncompressed(reference, _document.HeaderOffset + offsetValue, false);
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

        TryAddObjectIndexEntry(in reference, info);
        return true;
    }

    #endregion

    #region XRef Stream (PDF 1.5+)

    /// <summary>
    /// Applies the entries of a cross-reference stream reached other than by following the /Prev chain:
    /// the <c>/XRefStm</c> of a hybrid-reference file, or one the recovery scan found in the file.
    /// Entries the index already holds keep precedence, so this only fills the gaps — chiefly the
    /// compressed objects, which live inside /ObjStm containers and are never declared with an
    /// <c>N G obj</c> header of their own.
    /// </summary>
    /// <param name="crossReferenceStream">A parsed object whose dictionary is of <c>/Type /XRef</c>.</param>
    public void ApplyCrossReferenceStream(PdfObject crossReferenceStream)
    {
        ReadOnlyMemory<byte> decoded = crossReferenceStream.DecodeAsMemory();
        if (decoded.IsEmpty)
        {
            _logger.LogWarning("Cross-reference stream {Reference} decoded to nothing.", crossReferenceStream.Reference);
            return;
        }

        ParseXrefStreamEntries(crossReferenceStream.Dictionary, decoded);
    }

    /// <summary>
    /// Reads the cross-reference stream a classic section points at with <c>/XRefStm</c>, taking only
    /// its entries: the section's own trailer remains the one that carries /Root and /Prev, and the
    /// stream is read after the table it accompanies so the table keeps precedence over it.
    /// </summary>
    private void ApplyHybridCrossReferenceStream(PdfDictionary? trailer)
    {
        int? declaredOffset = _trailerParser.GetCrossReferenceStreamOffset(trailer);
        if (declaredOffset == null)
        {
            return;
        }

        int streamOffset = _document.HeaderOffset + declaredOffset.Value;
        if (streamOffset >= _document.Stream.Length)
        {
            _logger.LogWarning("Trailer declares /XRefStm at offset {Offset}, past the end of the file.", declaredOffset.Value);
            return;
        }

        PdfParser streamParser = new(_document.Stream, _document, allowReferences: true, decrypt: false);
        streamParser.Position = streamOffset;

        PdfObject? crossReferenceStream = streamParser.ReadObject();
        if (crossReferenceStream == null)
        {
            _logger.LogWarning("Trailer declares /XRefStm at offset {Offset}, where no object could be read.", declaredOffset.Value);
            return;
        }

        ApplyCrossReferenceStream(crossReferenceStream);
    }

    private PdfDictionary? ParseXrefStream(ref PdfParser parser)
    {
        PdfObject? xrefObject = parser.ReadObject();
        if (xrefObject == null || xrefObject.Dictionary == null)
        {
            _logger.LogDebug("startxref offset {Offset} did not yield a dictionary stream object.", parser.Position);
            return null;
        }

        ReadOnlyMemory<byte> decoded = xrefObject.DecodeAsMemory();
        if (decoded.IsEmpty)
        {
            _logger.LogWarning("Decoded xref stream empty.");
            return null;
        }

        ParseXrefStreamEntries(xrefObject.Dictionary, decoded);
        TryApplyTrailer(xrefObject.Dictionary);
        return xrefObject.Dictionary;
    }

    private void ParseXrefStreamEntries(PdfDictionary dict, in ReadOnlyMemory<byte> decoded)
    {
        PdfArray? wArray = dict.GetArray(PdfTokens.WKey);
        if (wArray == null || wArray.Count < 3)
        {
            _logger.LogWarning("XRef stream missing /W array.");
            return;
        }

        int w0 = wArray.GetIntegerOrDefault(0);
        int w1 = wArray.GetIntegerOrDefault(1);
        int w2 = wArray.GetIntegerOrDefault(2);
        if (w0 < 0 || w1 < 0 || w2 < 0)
        {
            _logger.LogWarning("Invalid negative /W widths.");
            return;
        }

        int entrySize = w0 + w1 + w2;
        if (entrySize <= 0)
        {
            _logger.LogWarning("Computed xref stream entry size is zero.");
            return;
        }

        PdfArray? indexArray = dict.GetArray(PdfTokens.IndexKey);
        List<(int start, int count)> ranges = [];
        if (indexArray?.Count >= 2 && indexArray.Count % 2 == 0)
        {
            for (int rangeIndex = 0; rangeIndex < indexArray.Count; rangeIndex += 2)
            {
                int start = indexArray.GetIntegerOrDefault(rangeIndex);
                int count = indexArray.GetIntegerOrDefault(rangeIndex + 1);
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
            _logger.LogWarning("No ranges to iterate in xref stream.");
            return;
        }

        ReadOnlySpan<byte> span = decoded.Span;
        int position = 0;
        foreach ((int start, int count) in ranges)
        {
            for (int localIndex = 0; localIndex < count; localIndex++)
            {
                if (position + entrySize > span.Length)
                {
                    _logger.LogWarning("Truncated xref stream (needed {Need} got {Rem}).", entrySize, span.Length - position);
                    return;
                }

                long type = (w0 == 0) ? 1 : ReadBigEndian(span.Slice(position, w0));
                position += w0;
                long field2 = (w1 == 0) ? 0 : ReadBigEndian(span.Slice(position, w1));
                position += w1;
                long field3 = (w2 == 0) ? 0 : ReadBigEndian(span.Slice(position, w2));
                position += w2;
                var objNumber = (uint)(start + localIndex);
                PdfReference reference = new(objNumber, (type == 1) ? (int)field3 : ((type == 0) ? (int)field3 : 0));
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
                        info = PdfObjectInfo.ForUncompressed(reference, _document.HeaderOffset + field2, true);
                        break;
                    }
                    case 2:
                    {
                        if (field2 == 0)
                        {
                            continue;
                        }

                        info = PdfObjectInfo.ForCompressed(reference, (uint)field2, (int)field3, true);
                        break;
                    }
                    default:
                    {
                        _logger.LogWarning("Unsupported xref stream entry type {Type} for object {Obj} (fields {F2},{F3}).", type, objNumber, field2, field3);
                        continue;
                    }
                }

                TryAddObjectIndexEntry(in reference, info);
            }
        }
    }

    /// <summary>
    /// Add an entry to the document object index if not already present (newest wins).
    /// </summary>
    /// <param name="reference">Object reference (number + generation).</param>
    /// <param name="info">Parsed xref information describing the object.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TryAddObjectIndexEntry(in PdfReference reference, PdfObjectInfo info)
    {
        if (!_document.ObjectCache.ObjectIndex.ContainsKey(reference))
        {
            _document.ObjectCache.ObjectIndex[reference] = info;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ReadBigEndian(in ReadOnlySpan<byte> slice)
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
            _document.RootObject = dict.GetObject(PdfTokens.RootKey);
        }

        _trailerParser.TrySetDecryptor(dict);
    }

    #endregion

    #region Shared Helpers

    private long LocateLastStartXref() => PdfByteScanner.LocateLast(_document.Stream, PdfTokens.Startxref);

    private int ParseStartXrefOffset(long startxrefPos)
    {
        PdfParser parser = new(_document.Stream, _document, allowReferences: false, decrypt: false);
        parser.Position = (int)startxrefPos + PdfTokens.Startxref.Length;
        IPdfValue? value = parser.ReadNextValue();

        if (value == null || value.Type != PdfValueType.Integer)
        {
            return -1;
        }

        return value.AsInteger();
    }

    private bool MatchSequenceAt(long position, in ReadOnlySpan<byte> sequence) => PdfByteScanner.MatchesAt(_document.Stream, position, sequence);

    #endregion
}
