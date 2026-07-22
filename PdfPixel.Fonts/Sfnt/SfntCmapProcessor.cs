using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads the binary form of the SFNT "cmap" table. A parsed <see cref="SfntCmap"/> is read-only -
/// edits to it are never written back, "cmap" is always passed through unchanged as raw bytes.
/// <see cref="CreateEmptyStub"/> covers the other case: synthesizing a placeholder "cmap" from
/// scratch for a brand new font that has none, since a valid OTTO container requires one.
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
    /// Parses a "cmap" table from its raw content bytes. Returns null if it is shorter than the
    /// fixed 4-byte header. Individual subtable records that don't fit are skipped rather than
    /// failing the whole table.
    /// </summary>
    public SfntCmap? Read(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < HeaderLength)
        {
            _logger.LogWarning("Failed to read 'cmap' table: expected at least {ExpectedLength} bytes, got {ActualLength}.", HeaderLength, data.Length);
            return null;
        }

        SfntReader headerReader = new(data.Span);
        headerReader.Skip(2); // version
        ushort numSubtables = headerReader.ReadUInt16OrDefault();

        List<SfntCmapSubtable> subtables = new(numSubtables);
        for (int subtableIndex = 0; subtableIndex < numSubtables; subtableIndex++)
        {
            SfntReader recordReader = new(data.Span);
            int recordOffset = HeaderLength + (subtableIndex * RecordLength);
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

            SfntReader formatReader = new(data.Span);
            if (!formatReader.Seek((int)subtableOffset))
            {
                _logger.LogWarning("Failed to read 'cmap' subtable {SubtableIndex}: subtable offset {SubtableOffset} is out of bounds.", subtableIndex, subtableOffset);
                continue;
            }

            ushort format = formatReader.ReadUInt16OrDefault();
            PdfFontEncoding? encoding = GetEncoding(platformId, encodingId);
            SortedDictionary<int, ushort>? codeToGid = ParseSubtable(data.Span, format, (int)subtableOffset);

            subtables.Add(new SfntCmapSubtable(format, platformId, encodingId, encoding, codeToGid));
        }

        return new SfntCmap { Subtables = subtables };
    }

    private static SortedDictionary<int, ushort>? ParseSubtable(in ReadOnlySpan<byte> data, ushort format, int offset)
    {
        return format switch
        {
            0 => ParseFormat0(data, offset),
            4 => ParseFormat4(data, offset),
            6 => ParseFormat6(data, offset),
            10 => ParseFormat10(data, offset),
            12 => ParseFormat12Or13(data, offset, isFormat13: false),
            13 => ParseFormat12Or13(data, offset, isFormat13: true),
            _ => null
        };
    }

    private static SortedDictionary<int, ushort> ParseFormat0(in ReadOnlySpan<byte> data, int offset)
    {
        SortedDictionary<int, ushort> codeToGid = [];

        SfntReader reader = new(data);
        if (!reader.Seek(offset + 6))
        {
            return codeToGid;
        }

        for (int code = 0; code < 256; code++)
        {
            byte glyphId = reader.ReadByteOrDefault();
            if (glyphId != 0)
            {
                codeToGid[code] = glyphId;
            }
        }

        return codeToGid;
    }

    private static SortedDictionary<int, ushort> ParseFormat4(in ReadOnlySpan<byte> data, int offset)
    {
        SortedDictionary<int, ushort> unicodeToGid = [];

        SfntReader reader = new(data);
        if (!reader.Seek(offset + 6))
        {
            return unicodeToGid;
        }

        int segCount = reader.ReadUInt16OrDefault() / 2;
        if (segCount == 0)
        {
            return unicodeToGid;
        }

        int endCodeOffset = offset + 14;
        int startCodeOffset = endCodeOffset + 2 + (segCount * 2);
        int idDeltaOffset = startCodeOffset + (segCount * 2);
        int idRangeOffsetOffset = idDeltaOffset + (segCount * 2);
        int glyphIdArrayOffset = idRangeOffsetOffset + (segCount * 2);

        for (int segmentIndex = 0; segmentIndex < segCount; segmentIndex++)
        {
            SfntReader segmentReader = new(data);

            segmentReader.Seek(endCodeOffset + (segmentIndex * 2));
            ushort endCode = segmentReader.ReadUInt16OrDefault();

            segmentReader.Seek(startCodeOffset + (segmentIndex * 2));
            ushort startCode = segmentReader.ReadUInt16OrDefault();

            segmentReader.Seek(idDeltaOffset + (segmentIndex * 2));
            var idDelta = (short)segmentReader.ReadUInt16OrDefault();

            segmentReader.Seek(idRangeOffsetOffset + (segmentIndex * 2));
            ushort idRangeOffset = segmentReader.ReadUInt16OrDefault();

            for (int code = startCode; code <= endCode; code++)
            {
                ushort glyphId;
                if (idRangeOffset == 0)
                {
                    glyphId = (ushort)((code + idDelta) & 0xFFFF);
                }
                else
                {
                    int rangeOffset = idRangeOffset / 2;
                    int glyphIndex = code - startCode + rangeOffset + (segmentIndex - segCount);

                    SfntReader glyphReader = new(data);
                    glyphReader.Seek(glyphIdArrayOffset + (glyphIndex * 2));
                    ushort glyphIdFromArray = glyphReader.ReadUInt16OrDefault();
                    glyphId = (ushort)((glyphIdFromArray + idDelta) & 0xFFFF);
                }

                if (glyphId != 0)
                {
                    unicodeToGid[code] = glyphId;
                }
            }
        }

        return unicodeToGid;
    }

    private static SortedDictionary<int, ushort> ParseFormat6(in ReadOnlySpan<byte> data, int offset)
    {
        SortedDictionary<int, ushort> codeToGid = [];

        SfntReader reader = new(data);
        if (!reader.Seek(offset + 6))
        {
            return codeToGid;
        }

        ushort firstCode = reader.ReadUInt16OrDefault();
        ushort entryCount = reader.ReadUInt16OrDefault();

        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            ushort glyphId = reader.ReadUInt16OrDefault();
            if (glyphId != 0)
            {
                codeToGid[firstCode + entryIndex] = glyphId;
            }
        }

        return codeToGid;
    }

    private static SortedDictionary<int, ushort> ParseFormat10(in ReadOnlySpan<byte> data, int offset)
    {
        SortedDictionary<int, ushort> codeToGid = [];

        SfntReader reader = new(data);
        if (!reader.Seek(offset + 12))
        {
            return codeToGid;
        }

        uint startCharCode = reader.ReadUInt32OrDefault();
        uint numChars = reader.ReadUInt32OrDefault();

        for (uint entryIndex = 0; entryIndex < numChars; entryIndex++)
        {
            ushort glyphId = reader.ReadUInt16OrDefault();
            uint code = startCharCode + entryIndex;
            if (glyphId != 0 && code <= int.MaxValue)
            {
                codeToGid[(int)code] = glyphId;
            }
        }

        return codeToGid;
    }

    private static SortedDictionary<int, ushort> ParseFormat12Or13(in ReadOnlySpan<byte> data, int offset, bool isFormat13)
    {
        SortedDictionary<int, ushort> codeToGid = [];

        SfntReader headerReader = new(data);
        if (!headerReader.Seek(offset + 12))
        {
            return codeToGid;
        }

        uint groupCount = headerReader.ReadUInt32OrDefault();
        int groupsOffset = offset + 16;

        for (uint groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            SfntReader groupReader = new(data);
            if (!groupReader.Seek(groupsOffset + ((int)groupIndex * 12)))
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

            for (uint charCode = startCharCode; charCode <= endCharCode && charCode <= int.MaxValue; charCode++)
            {
                uint glyphId = isFormat13 ? startGlyphId : startGlyphId + (charCode - startCharCode);
                if (glyphId != 0 && glyphId <= ushort.MaxValue)
                {
                    codeToGid[(int)charCode] = (ushort)glyphId;
                }
            }
        }

        return codeToGid;
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
