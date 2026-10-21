using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Parsing;

/// <summary>
/// Parses the cross-reference sections of a file, classic tables and cross-reference streams, from the
/// section <c>startxref</c> points at along the /Prev chain.
/// </summary>
internal sealed class PdfCrossReferenceParser
{
    private static ReadOnlySpan<byte> Xref => "xref"u8;

    private static ReadOnlySpan<byte> Startxref => "startxref"u8;

    private readonly IPdfDocumentInternal _document;
    private readonly ILogger<PdfCrossReferenceParser> _logger;

    public PdfCrossReferenceParser(IPdfDocumentInternal document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        _document = document;
        _logger = document.LoggerFactory.CreateLogger<PdfCrossReferenceParser>();
    }

    /// <summary>
    /// Parses the cross-reference sections of the file and merges them.
    /// </summary>
    /// <exception cref="PdfInvalidDocumentException">Thrown if a section fails to parse.</exception>
    public PdfCrossReference Parse()
    {
        List<PdfCrossReferenceSection> sections = ParseSections();

        return PdfCrossReference.FromSections(sections);
    }

    /// <summary>
    /// Applies the entries of a cross-reference stream reached other than by following the /Prev chain:
    /// the <c>/XRefStm</c> of a hybrid-reference file, or one the recovery scan found in the file.
    /// Entries the index already holds keep precedence, so this only fills the gaps — chiefly the
    /// compressed objects, which live inside /ObjStm containers and are never declared with an
    /// <c>N G obj</c> header of their own.
    /// </summary>
    /// <param name="crossReferenceStream">A parsed object whose dictionary is of <c>/Type /XRef</c>.</param>
    /// <param name="entries">The index the entries are added to.</param>
    public void ApplyCrossReferenceStream(PdfObject crossReferenceStream, Dictionary<PdfReference, PdfObjectInfo> entries)
    {
        ReadOnlyMemory<byte> decoded = crossReferenceStream.Stream.DecodeAsMemory();
        if (decoded.IsEmpty)
        {
            _logger.LogWarning("Cross-reference stream {Reference} decoded to nothing.", crossReferenceStream.Reference);
            return;
        }

        ParseXrefStreamEntries(crossReferenceStream.Dictionary, decoded, entries);
    }

    /// <summary>
    /// Parses the cross-reference sections of the file, newest first.
    /// </summary>
    private List<PdfCrossReferenceSection> ParseSections()
    {
        List<PdfCrossReferenceSection> sections = [];

        long startxrefPos = LocateLastStartXref();
        if (startxrefPos < 0)
        {
            _logger.LogError("'startxref' keyword not found.");
            return sections;
        }

        int declaredOffset = ParseStartXrefOffset(startxrefPos);
        int xrefOffset = _document.Header.Offset + declaredOffset;
        if (declaredOffset < 0 || xrefOffset >= _document.Stream.Length)
        {
            _logger.LogWarning("Parsed startxref offset {Offset} is invalid (file length {Length}).", declaredOffset, _document.Stream.Length);
            return sections;
        }

        PdfParser parser = new(_document.Stream, _document, PdfParserMode.CrossReference);
        HashSet<int> visitedOffsets = [xrefOffset];

        try
        {
            PdfCrossReferenceSection? section = ReadSection(ref parser, xrefOffset);
            while (section != null)
            {
                sections.Add(section);

                int? prevOffset = section.Trailer?.Prev;
                if (prevOffset == null)
                {
                    break;
                }

                int offsetValue = _document.Header.Offset + prevOffset.Value;
                if (!visitedOffsets.Add(offsetValue))
                {
                    _logger.LogWarning("Detected /Prev chain cycle at offset {Offset}; stopping xref traversal.", offsetValue);
                    break;
                }

                _logger.LogDebug("Following /Prev chain to offset {Offset}.", offsetValue);
                section = ReadSection(ref parser, offsetValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while parsing cross-reference section.");
            throw new PdfInvalidDocumentException("Failed to parse cross-reference section.", ex);
        }

        return sections;
    }

    /// <summary>
    /// Parses the section at <paramref name="offset"/>: a classic table (ISO 32000-2 7.5.4) or a cross-reference stream (7.5.8).
    /// </summary>
    private PdfCrossReferenceSection? ReadSection(ref PdfParser parser, int offset)
    {
        PdfCrossReferenceSection? section;
        if (MatchSequenceAt(offset, Xref))
        {
            section = ParseClassicXref(ref parser, offset);
        }
        else
        {
            section = ParseXrefStream(ref parser, offset);
        }

        return section;
    }

    /// <summary>
    /// Parses a classic table and the <c>trailer</c> dictionary after it (ISO 32000-2 7.5.4, 7.5.5).
    /// </summary>
    private PdfCrossReferenceSection ParseClassicXref(ref PdfParser parser, int offset)
    {
        parser.Position = offset + Xref.Length;

        Dictionary<PdfReference, PdfObjectInfo> entries = [];
        PdfDictionary? trailerDictionary = null;
        int subsectionIndex = 0;
        while (true)
        {
            PdfValue? firstValue = parser.ReadNextValue();
            if (firstValue == null)
            {
                _logger.LogDebug("Finished parsing classic xref (EOF).");
                break;
            }

            if (firstValue is PdfKeywordValue keywordValue)
            {
                if (keywordValue.Value == PdfKeyword.Trailer)
                {
                    PdfValue? dictValue = parser.ReadNextValue();
                    trailerDictionary = dictValue?.AsDictionary();

                    _logger.LogTrace("Encountered 'trailer' after subsection {Index}. Ending xref parse.", subsectionIndex);
                    break;
                }

                _logger.LogDebug("Unexpected keyword instead of subsection header; ending parse.");
                break;
            }

            int? subsectionStart = firstValue.AsInteger();
            if (subsectionStart == null)
            {
                _logger.LogDebug("Non-integer subsection start value; ending classic xref parse.");
                break;
            }

            int? subsectionCount = parser.ReadNextValue().AsInteger();
            if (subsectionCount == null)
            {
                _logger.LogWarning("Failed to read entry count for subsection {Index} (start {First}) at position {Pos}.", subsectionIndex, subsectionStart.Value, parser.Position);
                break;
            }

            int firstObject = subsectionStart.Value;
            int entryCount = subsectionCount.Value;
            int parsedCount = 0;

            for (int localIndex = 0; localIndex < entryCount; localIndex++)
            {
                var entryObjectNumber = (uint)(firstObject + localIndex);
                if (!ParseSingleEntry(ref parser, entryObjectNumber, entries))
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

        PdfTrailer? trailer = null;
        if (trailerDictionary != null)
        {
            trailer = new PdfTrailer(trailerDictionary);
            ApplyHybridCrossReferenceStream(trailer, entries);
        }

        return new PdfCrossReferenceSection(entries, trailer);
    }

    /// <summary>
    /// Parses one table entry, offset, generation and <c>n</c> or <c>f</c>; restores the position when it fails.
    /// </summary>
    private bool ParseSingleEntry(ref PdfParser parser, uint objectNumber, Dictionary<PdfReference, PdfObjectInfo> entries)
    {
        int entryStart = parser.Position;

        int? offsetValue = parser.ReadNextValue().AsInteger();
        int? generation = parser.ReadNextValue().AsInteger();
        PdfValue? statusValue = parser.ReadNextValue();

        if (offsetValue == null || generation == null || statusValue is not PdfKeywordValue statusKeyword)
        {
            parser.Position = entryStart;
            return false;
        }

        PdfReference reference = new(objectNumber, generation.Value);
        PdfObjectInfo info;
        if (statusKeyword.Value == PdfKeyword.InUse)
        {
            info = PdfObjectInfo.ForUncompressed(_document.Header.Offset + (uint)offsetValue.Value);
        }
        else if (statusKeyword.Value == PdfKeyword.Free)
        {
            info = PdfObjectInfo.Free;
        }
        else
        {
            parser.Position = entryStart;
            return false;
        }

        TryAddEntry(entries, in reference, info);
        return true;
    }

    /// <summary>
    /// Adds the entries of the /XRefStm cross-reference stream of a hybrid-reference file (ISO 32000-2 7.5.8.4).
    /// </summary>
    private void ApplyHybridCrossReferenceStream(PdfTrailer trailer, Dictionary<PdfReference, PdfObjectInfo> entries)
    {
        int? declaredOffset = trailer.XRefStm;
        if (declaredOffset == null)
        {
            return;
        }

        int streamOffset = _document.Header.Offset + declaredOffset.Value;
        if (streamOffset >= _document.Stream.Length)
        {
            _logger.LogWarning("Trailer declares /XRefStm at offset {Offset}, past the end of the file.", declaredOffset.Value);
            return;
        }

        PdfParser streamParser = new(_document.Stream, _document, PdfParserMode.CrossReference);
        streamParser.Position = streamOffset;

        PdfObject? crossReferenceStream = streamParser.ReadObject();
        if (crossReferenceStream == null)
        {
            _logger.LogWarning("Trailer declares /XRefStm at offset {Offset}, where no object could be read.", declaredOffset.Value);
            return;
        }

        ApplyCrossReferenceStream(crossReferenceStream, entries);
    }

    /// <summary>
    /// Parses a cross-reference stream; its dictionary is the trailer (ISO 32000-2 7.5.8).
    /// </summary>
    private PdfCrossReferenceSection? ParseXrefStream(ref PdfParser parser, int offset)
    {
        parser.Position = offset;

        PdfObject? xrefObject = parser.ReadObject();
        if (xrefObject == null || xrefObject.Dictionary == null)
        {
            _logger.LogDebug("startxref offset {Offset} did not yield a dictionary stream object.", parser.Position);
            return null;
        }

        ReadOnlyMemory<byte> decoded = xrefObject.Stream.DecodeAsMemory();
        if (decoded.IsEmpty)
        {
            _logger.LogWarning("Decoded xref stream empty.");
            return null;
        }

        Dictionary<PdfReference, PdfObjectInfo> entries = [];
        ParseXrefStreamEntries(xrefObject.Dictionary, decoded, entries);

        return new PdfCrossReferenceSection(entries, new PdfTrailer(xrefObject.Dictionary));
    }

    /// <summary>
    /// Reads the /W-sized entries over the /Index ranges, by default <c>[0 Size]</c> (ISO 32000-2 7.5.8.2, 7.5.8.3).
    /// </summary>
    private void ParseXrefStreamEntries(PdfDictionary dict, in ReadOnlyMemory<byte> decoded, Dictionary<PdfReference, PdfObjectInfo> entries)
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
                        info = PdfObjectInfo.Free;
                        break;
                    }
                    case 1:
                    {
                        info = PdfObjectInfo.ForUncompressed(_document.Header.Offset + field2);
                        break;
                    }
                    case 2:
                    {
                        if (field2 == 0)
                        {
                            continue;
                        }

                        info = PdfObjectInfo.ForCompressed((uint)field2, (int)field3);
                        break;
                    }
                    default:
                    {
                        _logger.LogWarning("Unsupported xref stream entry type {Type} for object {Obj} (fields {F2},{F3}).", type, objNumber, field2, field3);
                        continue;
                    }
                }

                TryAddEntry(entries, in reference, info);
            }
        }
    }

    private long LocateLastStartXref() => PdfByteScanner.LocateLast(_document.Stream, Startxref);

    private int ParseStartXrefOffset(long startxrefPos)
    {
        PdfParser parser = new(_document.Stream, _document, PdfParserMode.CrossReference);
        parser.Position = (int)startxrefPos + Startxref.Length;

        // -1 marks a missing or non-numeric startxref offset.
        return parser.ReadNextValue().AsInteger() ?? -1;
    }

    private bool MatchSequenceAt(long position, in ReadOnlySpan<byte> sequence) => PdfByteScanner.MatchesAt(_document.Stream, position, sequence);

    /// <summary>
    /// Adds the entry unless <paramref name="entries"/> already has one for <paramref name="reference"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TryAddEntry(Dictionary<PdfReference, PdfObjectInfo> entries, in PdfReference reference, PdfObjectInfo info)
    {
        if (!entries.ContainsKey(reference))
        {
            entries[reference] = info;
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
}
