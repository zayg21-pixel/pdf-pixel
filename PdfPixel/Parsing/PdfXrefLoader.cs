using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using System.IO;
using System.Text;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

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
    private readonly BinaryReader _reader; // Reusable reader for frequent byte access.

    public PdfXrefLoader(PdfDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        _document = document;
        _logger = document.LoggerFactory.CreateLogger<PdfXrefLoader>();
        _trailerParser = new PdfTrailerParser(document);
        _reader = new BinaryReader(document.Stream, Encoding.ASCII, leaveOpen: true);
    }

    /// <summary>
    /// Loads the cross-reference table/stream(s) into the document's object index.
    /// </summary>
    public void LoadXref()
    {
        long startxrefPos = LocateLastStartXref();
        if (startxrefPos < 0)
        {
            _logger.LogWarning("PdfXrefLoader: 'startxref' keyword not found – falling back to legacy full scan.");
            return;
        }

        int xrefOffset = ParseStartXrefOffset(startxrefPos);
        if (xrefOffset < 0 || xrefOffset >= _document.Stream.Length)
        {
            _logger.LogWarning("PdfXrefLoader: Parsed startxref offset {Offset} is invalid (file length {Length}).", xrefOffset, _document.Stream.Length);
            return;
        }
        var parser = new PdfParser(_document.Stream, _document, allowReferences: true, decrypt: false);

        // Classic table path.
        if (MatchSequenceAt(xrefOffset, PdfTokens.Xref))
        {
            try
            {
                parser.Position = xrefOffset + PdfTokens.Xref.Length;
                PdfDictionary trailer = ParseClassicXref(ref parser);

                // Walk /Prev chain backwards.
                int? prevOffset;
                while ((prevOffset = _trailerParser.GetPrevOffset(trailer)).HasValue)
                {
                    int offsetValue = prevOffset.Value;
                    _logger.LogDebug("PdfXrefLoader: Following /Prev chain to offset {Offset} (classic path).", offsetValue);
                    if (MatchSequenceAt(offsetValue, PdfTokens.Xref))
                    {
                        parser.Position = offsetValue + PdfTokens.Xref.Length;
                        trailer = ParseClassicXref(ref parser);
                    }
                    else
                    {
                        var streamParser = new PdfParser(_document.Stream, _document, allowReferences: true, decrypt: false);
                        streamParser.Position = offsetValue;
                        trailer = ParseXrefStream(ref streamParser);
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
            parser.Position = xrefOffset;
            PdfDictionary streamTrailer = ParseXrefStream(ref parser);

            int? prevOffset;
            while ((prevOffset = _trailerParser.GetPrevOffset(streamTrailer)).HasValue)
            {
                int offsetValue = prevOffset.Value;
                _logger.LogDebug("PdfXrefLoader: Following /Prev chain to offset {Offset} (stream path).", offsetValue);
                if (MatchSequenceAt(offsetValue, PdfTokens.Xref))
                {
                    parser.Position = offsetValue + PdfTokens.Xref.Length;
                    streamTrailer = ParseClassicXref(ref parser);
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
                uint entryObjectNumber = (uint)(firstObject + localIndex);
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
    private bool ParseSingleEntry(ref PdfParser parser, uint objectNumber)
    {
        int entryStart = parser.Position;

        uint offsetValue = (uint)parser.ReadNextValue().AsInteger();
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

        var decoded = xrefObject.DecodeAsMemory();
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
        int w0 = wArray.GetIntegerOrDefault(0);
        int w1 = wArray.GetIntegerOrDefault(1);
        int w2 = wArray.GetIntegerOrDefault(2);
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
                uint objNumber = (uint)(start + localIndex);
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
                        info = PdfObjectInfo.ForCompressed(reference, (uint)field2, (int)field3, true);
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
        if (!_document.ObjectCache.ObjectIndex.ContainsKey(reference))
        {
            _document.ObjectCache.ObjectIndex[reference] = info;
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
            _document.RootObject = dict.GetObject(PdfTokens.RootKey);
        }

        _trailerParser.TrySetDecryptor(dict);
    }
    #endregion

    #region Shared Helpers
    private long LocateLastStartXref()
    {
        ReadOnlySpan<byte> token = PdfTokens.Startxref;
        for (long scanIndex = _document.Stream.Length - token.Length; scanIndex >= 0; scanIndex--)
        {
            if (MatchSequenceAt(scanIndex, token))
            {
                return scanIndex;
            }
        }
        return -1;
    }

    private int ParseStartXrefOffset(long startxrefPos)
    {
        PdfParser parser = new PdfParser(_document.Stream, _document, allowReferences: false, decrypt: false);
        parser.Position = (int)startxrefPos + PdfTokens.Startxref.Length;
        var value = parser.ReadNextValue();

        if (value.Type != PdfValueType.Integer)
        {
            return -1;
        }

        return value.AsInteger();
    }

    /// <summary>
    /// Match a byte sequence at the specified absolute file position using the underlying FileStream.
    /// Seeks to the provided position, reads the required bytes and advances the stream; does not restore position.
    /// </summary>
    /// <param name="position">Absolute byte offset in the PDF file.</param>
    /// <param name="sequence">Sequence to compare.</param>
    /// <returns>True when the bytes at the specified position equal the sequence.</returns>
    private bool MatchSequenceAt(long position, ReadOnlySpan<byte> sequence)
    {
        if (sequence.Length == 0)
        {
            return true;
        }

        Stream stream = _reader.BaseStream;
        if (position < 0)
        {
            return false;
        }
        if (position + sequence.Length > stream.Length)
        {
            return false;
        }

        stream.Position = position;

        byte[] buffer = new byte[sequence.Length];
        int bytesRead = _reader.Read(buffer, 0, buffer.Length);
        if (bytesRead != buffer.Length)
        {
            return false;
        }

        return new ReadOnlySpan<byte>(buffer).SequenceEqual(sequence);
    }
    #endregion
}
