using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads the binary form of the SFNT "cmap" table. <see cref="Read"/> parses only the directory
/// (format/platform/encoding/offset per subtable); a subtable's actual mapping is parsed lazily by
/// <see cref="GetGid"/> on first query and cached on the <see cref="SfntCmapSubtable"/> itself, so a
/// subtable nothing ever queries is never parsed. A parsed <see cref="SfntCmap"/> is read-only - edits
/// to it are never written back, "cmap" is always passed through unchanged as raw bytes.
/// <see cref="CreateEmptyStub"/> covers the other case: synthesizing a placeholder "cmap" from scratch
/// for a brand new font that has none, since a valid OTTO container requires one.
/// </summary>
public class SfntCmapProcessor
{
    private const int HeaderLength = 4;
    private const int RecordLength = 8;

    private const ushort EmptyStubPlatformId = 3;
    private const ushort EmptyStubEncodingId = 1;

    private readonly ILogger<SfntCmapProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntCmapProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntCmapProcessor(ILogger<SfntCmapProcessor> logger) => _logger = logger;

    /// <summary>
    /// Builds a minimal placeholder "cmap" table: a single Windows/Unicode-BMP (platform 3,
    /// encoding 1) format 4 subtable mapping no codepoints at all. For synthesizing a brand new
    /// font that needs a structurally valid "cmap" table but has no real character mapping to give
    /// it (e.g. a CFF font being wrapped in an OTTO container).
    /// </summary>
    public static byte[] CreateEmptyStub()
    {
        SfntWriter writer = new();

        writer.WriteUInt16(0); // version
        writer.WriteUInt16(1); // numTables
        writer.WriteUInt16(EmptyStubPlatformId);
        writer.WriteUInt16(EmptyStubEncodingId);
        writer.WriteUInt32(12); // offset to the subtable below

        // Format 4 subtable with a single, terminating segment that maps nothing.
        writer.WriteUInt16(4); // format
        writer.WriteUInt16(24); // length
        writer.WriteUInt16(0); // language
        writer.WriteUInt16(2); // segCountX2 (1 segment)
        writer.WriteUInt16(2); // searchRange
        writer.WriteUInt16(0); // entrySelector
        writer.WriteUInt16(0); // rangeShift
        writer.WriteUInt16(0xFFFF); // endCode
        writer.WriteUInt16(0); // reservedPad
        writer.WriteUInt16(0xFFFF); // startCode
        writer.WriteUInt16(1); // idDelta
        writer.WriteUInt16(0); // idRangeOffset

        return writer.ToArray();
    }

    /// <summary>
    /// Parses a "cmap" table's directory only: every subtable's format, platform/encoding, and byte
    /// offset. Returns null if the directory is shorter than the fixed 4-byte header. Individual
    /// subtable records that don't fit are skipped rather than failing the whole table.
    /// </summary>
    public SfntCmap? Read(in SfntCmapSource source)
    {
        if (source.CmapRecord.Length < HeaderLength)
        {
            _logger.LogWarning("Failed to read 'cmap' table: expected at least {ExpectedLength} bytes, got {ActualLength}.", HeaderLength, source.CmapRecord.Length);
            return null;
        }

        SfntReader headerReader = new(GetSubtableMemory(source, 0, HeaderLength).Span);
        headerReader.Skip(2); // version
        ushort numSubtables = headerReader.ReadUInt16OrDefault();

        ReadOnlyMemory<byte> directoryData = GetSubtableMemory(source, HeaderLength, numSubtables * RecordLength);

        List<SfntCmapSubtable> subtables = new(numSubtables);
        for (int subtableIndex = 0; subtableIndex < numSubtables; subtableIndex++)
        {
            SfntReader recordReader = new(directoryData.Span);
            int recordOffset = subtableIndex * RecordLength;
            if (!recordReader.Seek(recordOffset))
            {
                _logger.LogWarning("Failed to read 'cmap' subtable record {SubtableIndex}: record offset {RecordOffset} is out of bounds.", subtableIndex, recordOffset);
                break;
            }

            ushort platformId = recordReader.ReadUInt16OrDefault();
            ushort encodingId = recordReader.ReadUInt16OrDefault();
            uint subtableOffset = recordReader.ReadUInt32OrDefault();

            if (!recordReader.IsValid)
            {
                _logger.LogWarning("Failed to read 'cmap' subtable record {SubtableIndex}: record is truncated.", subtableIndex);
                break;
            }

            ReadOnlyMemory<byte> formatBytes = GetSubtableMemory(source, (int)subtableOffset, 2);
            if (formatBytes.Length < 2)
            {
                _logger.LogWarning("Failed to read 'cmap' subtable {SubtableIndex}: subtable offset {SubtableOffset} is out of bounds.", subtableIndex, subtableOffset);
                continue;
            }

            SfntReader formatReader = new(formatBytes.Span);
            ushort format = formatReader.ReadUInt16OrDefault();
            PdfFontEncoding? encoding = GetEncoding(platformId, encodingId);

            subtables.Add(new SfntCmapSubtable(format, platformId, encodingId, encoding, (int)subtableOffset));
        }

        return new SfntCmap { Subtables = subtables };
    }

    /// <summary>
    /// Resolves a character code to a glyph id via <paramref name="subtable"/>, parsing and caching
    /// its ranges from <paramref name="source"/> first if this is the first query against it.
    /// </summary>
    /// <param name="subtable">The subtable to query.</param>
    /// <param name="code">The character code to resolve.</param>
    /// <param name="source">The stream and table range to parse <paramref name="subtable"/> from, if not already resolved.</param>
    public ushort? GetGid(SfntCmapSubtable subtable, int code, in SfntCmapSource source)
    {
        if (!subtable.IsResolved)
        {
            subtable.SetRanges(ParseSubtable(source, subtable.Format, subtable.SubtableOffset));
        }

        return subtable.GetGid(code);
    }

    private static ISfntCmapRange[] ParseSubtable(in SfntCmapSource source, ushort format, int offset)
    {
        return format switch
        {
            0 => ParseFormat0(source, offset),
            4 => ParseFormat4(source, offset),
            6 => ParseFormat6(source, offset),
            10 => ParseFormat10(source, offset),
            12 => ParseFormat12Or13(source, offset, isFormat13: false),
            13 => ParseFormat12Or13(source, offset, isFormat13: true),
            _ => []
        };
    }

    private static ISfntCmapRange[] ParseFormat0(in SfntCmapSource source, int offset)
    {
        byte[] array = GetSubtableBytes(source, offset + 6, 256);
        if (array.Length == 0)
        {
            return [];
        }

        return [new SfntCmapGlyphArrayRange(0, 255, idDelta: 0, array, entryByteWidth: 1)];
    }

    private static ISfntCmapRange[] ParseFormat4(in SfntCmapSource source, int offset)
    {
        ReadOnlyMemory<byte> segCountBytes = GetSubtableMemory(source, offset + 6, 2);
        if (segCountBytes.Length < 2)
        {
            return [];
        }

        SfntReader segCountReader = new(segCountBytes.Span);
        int segCount = segCountReader.ReadUInt16OrDefault() / 2;
        if (segCount == 0)
        {
            return [];
        }

        int endCodeOffset = offset + 14;
        int headerBlockLength = (segCount * 2 * 4) + 2; // endCode[] + reservedPad + startCode[] + idDelta[] + idRangeOffset[]

        ReadOnlyMemory<byte> headerBlock = GetSubtableMemory(source, endCodeOffset, headerBlockLength);
        if (headerBlock.Length < headerBlockLength)
        {
            return [];
        }

        int localStartCodeOffset = (segCount * 2) + 2;
        int localIdDeltaOffset = localStartCodeOffset + (segCount * 2);
        int localIdRangeOffsetOffset = localIdDeltaOffset + (segCount * 2);
        int glyphIdArrayOffset = endCodeOffset + headerBlockLength;

        List<ISfntCmapRange> ranges = new(segCount);
        for (int segmentIndex = 0; segmentIndex < segCount; segmentIndex++)
        {
            SfntReader segmentReader = new(headerBlock.Span);

            segmentReader.Seek(segmentIndex * 2);
            ushort endCode = segmentReader.ReadUInt16OrDefault();

            segmentReader.Seek(localStartCodeOffset + (segmentIndex * 2));
            ushort startCode = segmentReader.ReadUInt16OrDefault();

            segmentReader.Seek(localIdDeltaOffset + (segmentIndex * 2));
            var idDelta = (short)segmentReader.ReadUInt16OrDefault();

            segmentReader.Seek(localIdRangeOffsetOffset + (segmentIndex * 2));
            ushort idRangeOffset = segmentReader.ReadUInt16OrDefault();

            if (startCode > endCode)
            {
                continue;
            }

            if (idRangeOffset == 0)
            {
                ranges.Add(new SfntCmapDeltaRange(startCode, endCode, idDelta));
                continue;
            }

            int rangeOffset = idRangeOffset / 2;
            int glyphIndexAtStart = rangeOffset + segmentIndex - segCount;
            int arrayByteOffset = glyphIdArrayOffset + (glyphIndexAtStart * 2);
            int arrayByteLength = (endCode - startCode + 1) * 2;

            byte[] array = GetSubtableBytes(source, arrayByteOffset, arrayByteLength);
            if (array.Length == 0)
            {
                continue;
            }

            ranges.Add(new SfntCmapGlyphArrayRange(startCode, endCode, idDelta, array, entryByteWidth: 2));
        }

        return ranges.ToArray();
    }

    private static ISfntCmapRange[] ParseFormat6(in SfntCmapSource source, int offset)
    {
        ReadOnlyMemory<byte> headerBytes = GetSubtableMemory(source, offset + 6, 4);
        if (headerBytes.Length < 4)
        {
            return [];
        }

        SfntReader headerReader = new(headerBytes.Span);
        ushort firstCode = headerReader.ReadUInt16OrDefault();
        ushort entryCount = headerReader.ReadUInt16OrDefault();
        if (entryCount == 0)
        {
            return [];
        }

        byte[] array = GetSubtableBytes(source, offset + 10, entryCount * 2);
        if (array.Length == 0)
        {
            return [];
        }

        return [new SfntCmapGlyphArrayRange(firstCode, firstCode + entryCount - 1, idDelta: 0, array, entryByteWidth: 2)];
    }

    private static ISfntCmapRange[] ParseFormat10(in SfntCmapSource source, int offset)
    {
        ReadOnlyMemory<byte> headerBytes = GetSubtableMemory(source, offset + 12, 8);
        if (headerBytes.Length < 8)
        {
            return [];
        }

        SfntReader headerReader = new(headerBytes.Span);
        uint startCharCode = headerReader.ReadUInt32OrDefault();
        uint numChars = headerReader.ReadUInt32OrDefault();
        if (numChars == 0 || startCharCode > int.MaxValue)
        {
            return [];
        }

        long arrayByteLength = Math.Min((long)numChars * 2, int.MaxValue);
        byte[] array = GetSubtableBytes(source, offset + 20, (int)arrayByteLength);
        if (array.Length == 0)
        {
            return [];
        }

        long endCode = Math.Min((long)startCharCode + numChars - 1, int.MaxValue);

        return [new SfntCmapGlyphArrayRange((int)startCharCode, (int)endCode, idDelta: 0, array, entryByteWidth: 2)];
    }

    private static ISfntCmapRange[] ParseFormat12Or13(in SfntCmapSource source, int offset, bool isFormat13)
    {
        ReadOnlyMemory<byte> countBytes = GetSubtableMemory(source, offset + 12, 4);
        if (countBytes.Length < 4)
        {
            return [];
        }

        SfntReader countReader = new(countBytes.Span);
        uint groupCount = countReader.ReadUInt32OrDefault();
        long groupsByteLength = Math.Min((long)groupCount * 12, int.MaxValue);

        ReadOnlyMemory<byte> groupsData = GetSubtableMemory(source, offset + 16, (int)groupsByteLength);
        int availableGroups = groupsData.Length / 12;

        List<ISfntCmapRange> ranges = new(availableGroups);
        for (int groupIndex = 0; groupIndex < availableGroups; groupIndex++)
        {
            SfntReader groupReader = new(groupsData.Span);
            if (!groupReader.Seek(groupIndex * 12))
            {
                break;
            }

            uint startCharCode = groupReader.ReadUInt32OrDefault();
            uint endCharCode = groupReader.ReadUInt32OrDefault();
            uint startGlyphId = groupReader.ReadUInt32OrDefault();

            if (!groupReader.IsValid)
            {
                break;
            }

            if (startCharCode > endCharCode || endCharCode > int.MaxValue)
            {
                continue;
            }

            if (isFormat13)
            {
                if (startGlyphId <= ushort.MaxValue)
                {
                    ranges.Add(new SfntCmapConstantRange((int)startCharCode, (int)endCharCode, (ushort)startGlyphId));
                }
            }
            else
            {
                ranges.Add(new SfntCmapLinearGidRange((int)startCharCode, (int)endCharCode, (int)startGlyphId));
            }
        }

        return ranges.ToArray();
    }

    /// <summary>
    /// Reads <paramref name="length"/> bytes starting at <paramref name="relativeOffset"/> within the
    /// "cmap" table, clamped to the table's declared bounds. Returns an empty span if
    /// <paramref name="relativeOffset"/> itself is out of bounds.
    /// </summary>
    private static ReadOnlyMemory<byte> GetSubtableMemory(in SfntCmapSource source, int relativeOffset, int length)
    {
        int clampedLength = ClampLength(source, relativeOffset, length);
        return (clampedLength <= 0) ? ReadOnlyMemory<byte>.Empty : source.Stream.GetMemory(source.CmapRecord.Offset + relativeOffset, clampedLength);
    }

    /// <summary>
    /// Reads <paramref name="length"/> bytes starting at <paramref name="relativeOffset"/> within the
    /// "cmap" table, clamped to the table's declared bounds. Returns an empty array if
    /// <paramref name="relativeOffset"/> itself is out of bounds.
    /// </summary>
    private static byte[] GetSubtableBytes(in SfntCmapSource source, int relativeOffset, int length)
    {
        int clampedLength = ClampLength(source, relativeOffset, length);
        return (clampedLength <= 0) ? [] : source.Stream.GetBytes(source.CmapRecord.Offset + relativeOffset, clampedLength);
    }

    private static int ClampLength(in SfntCmapSource source, int relativeOffset, int length)
    {
        if (relativeOffset < 0 || relativeOffset >= source.CmapRecord.Length)
        {
            return 0;
        }

        return Math.Min(length, source.CmapRecord.Length - relativeOffset);
    }

    private static PdfFontEncoding? GetEncoding(ushort platformId, ushort encodingId)
    {
        // MacRoman: platform 1, encoding 0
        if (platformId == 1 && encodingId == 0)
        {
            return PdfFontEncoding.MacRomanEncoding;
        }

        // Symbol: platform 3, encoding 0
        if (platformId == 3 && encodingId == 0)
        {
            return PdfFontEncoding.SymbolEncoding;
        }

        // WinAnsi: platform 3, encoding 1
        if (platformId == 3 && encodingId == 1)
        {
            return PdfFontEncoding.WinAnsiEncoding;
        }

        // MacExpert: platform 1, encoding 2
        if (platformId == 1 && encodingId == 2)
        {
            return PdfFontEncoding.MacExpertEncoding;
        }

        // StandardEncoding: platform 1, encoding 1 (rare)
        if (platformId == 1 && encodingId == 1)
        {
            return PdfFontEncoding.StandardEncoding;
        }

        return null;
    }
}
