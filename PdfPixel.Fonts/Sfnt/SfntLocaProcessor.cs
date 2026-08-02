using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "loca" table. Like "hmtx", it is not self-describing:
/// <see cref="Read"/> needs the glyph count (from "maxp") and the offset format (from "head"'s
/// indexToLocFormat) as inputs, since "loca" encodes offsets either as halved UInt16 values (format 0)
/// or literal UInt32 values (format 1).
/// </summary>
public class SfntLocaProcessor
{
    private readonly ILogger<SfntLocaProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntLocaProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public SfntLocaProcessor(ILogger<SfntLocaProcessor> logger) => _logger = logger;

    /// <summary>
    /// Parses a "loca" table from its raw content bytes, resolving each glyph's byte range within
    /// "glyf". An entry missing past the end of the data defaults to offset 0, which
    /// <see cref="ResolveRanges"/> already treats as an empty glyph - the same convention a
    /// conforming font uses to mark one deliberately.
    /// </summary>
    /// <param name="data">The "loca" table's raw content bytes.</param>
    /// <param name="numGlyphs">The "maxp" table's numGlyphs field.</param>
    /// <param name="indexToLocFormat">The "head" table's indexToLocFormat field: 0 for short (halved
    /// UInt16) entries, any other value for long (UInt32) entries.</param>
    /// <param name="glyfDataLength">The "glyf" table's byte length, used both to clamp out-of-range
    /// offsets and as the fallback end for the last glyph.</param>
    public SfntLoca Read(in ReadOnlyMemory<byte> data, ushort numGlyphs, short indexToLocFormat, int glyfDataLength)
    {
        int entryCount = numGlyphs + 1;
        bool isLongFormat = indexToLocFormat != 0;
        int expectedLength = entryCount * (isLongFormat ? 4 : 2);

        if (data.Length < expectedLength)
        {
            _logger.LogWarning("Reading truncated 'loca' table: expected {ExpectedLength} bytes, got {ActualLength}; missing entries default to an empty glyph.", expectedLength, data.Length);
        }

        SfntReader reader = new(data.Span);
        var offsets = new uint[entryCount];
        for (int index = 0; index < entryCount; index++)
        {
            uint offset = isLongFormat ? reader.ReadUInt32OrDefault() : (uint)(reader.ReadUInt16OrDefault() * 2);
            offsets[index] = Math.Min(offset, (uint)glyfDataLength);
        }

        return new SfntLoca { Ranges = ResolveRanges(offsets, (uint)glyfDataLength) };
    }

    /// <summary>
    /// Writes a "loca" table's binary content in the format <paramref name="indexToLocFormat"/>
    /// specifies, which must be the same value written to <c>head.indexToLocFormat</c>.
    /// </summary>
    public byte[] Write(SfntLoca loca, short indexToLocFormat)
    {
        if (loca == null)
        {
            throw new ArgumentNullException(nameof(loca));
        }

        SfntWriter writer = new();
        bool isLongFormat = indexToLocFormat != 0;
        IReadOnlyList<SfntGlyphRange> ranges = loca.Ranges;
        for (int index = 0; index < ranges.Count; index++)
        {
            WriteOffset(writer, ranges[index].Offset, isLongFormat);
        }

        SfntGlyphRange lastRange = (ranges.Count > 0) ? ranges[ranges.Count - 1] : default;
        uint glyfDataLength = lastRange.Offset + lastRange.Length;
        WriteOffset(writer, glyfDataLength, isLongFormat);

        return writer.ToArray();
    }

    private static void WriteOffset(SfntWriter writer, uint offset, bool isLongFormat)
    {
        if (isLongFormat)
        {
            writer.WriteUInt32(offset);
        }
        else
        {
            writer.WriteUInt16((ushort)(offset / 2));
        }
    }

    /// <summary>
    /// Resolves each glyph's byte range from raw, already glyf-length-clamped loca offsets. The spec
    /// requires ascending offsets, but some fonts leave later entries out of order or use 0 to mark a
    /// glyph as missing; pairing each entry with its successor by offset order - rather than by index
    /// order - recovers the correct range even when the raw entries themselves are scrambled.
    /// </summary>
    private static SfntGlyphRange[] ResolveRanges(uint[] offsets, uint glyfDataLength)
    {
        int numGlyphs = offsets.Length - 1;
        if (numGlyphs <= 0)
        {
            return Array.Empty<SfntGlyphRange>();
        }

        var indicesByOffset = new int[offsets.Length];
        for (int index = 0; index < offsets.Length; index++)
        {
            indicesByOffset[index] = index;
        }

        Array.Sort(
            indicesByOffset,
            (left, right) =>
            {
                int comparison = offsets[left].CompareTo(offsets[right]);
                return (comparison != 0) ? comparison : left.CompareTo(right);
            });

        // Sized like offsets (numGlyphs + 1): the trailing entry doesn't correspond to a real glyph,
        // but since its offset participates in the sort like any other, it can land at any sorted
        // position - including one below numGlyphs - so it needs a slot here too.
        var endOffsets = new uint[offsets.Length];
        for (int sortedPosition = 0; sortedPosition < numGlyphs; sortedPosition++)
        {
            endOffsets[indicesByOffset[sortedPosition]] = offsets[indicesByOffset[sortedPosition + 1]];
        }

        // Multiple empty glyphs at the start all carry offset 0; give the last one in that run the
        // next real offset as its end, instead of leaving every one of them collapsed to zero length.
        for (int index = 0; index < numGlyphs; index++)
        {
            if (offsets[index] != 0 || endOffsets[index] != 0)
            {
                break;
            }

            uint nextOffset = offsets[index + 1];
            if (nextOffset == 0)
            {
                continue;
            }

            endOffsets[index] = nextOffset;
            break;
        }

        // Whichever entry holds the single largest offset has no successor to take an end from - that
        // is normally the trailing sentinel (which needs none), but a missing/zeroed sentinel can
        // displace the true last glyph into that spot instead. Fall back to the glyf table's total
        // length for it, unless it turns out to be the sentinel itself (no glyph to fix up).
        int lastSortedIndex = indicesByOffset[indicesByOffset.Length - 1];
        if (lastSortedIndex < numGlyphs && offsets[lastSortedIndex] != 0 && endOffsets[lastSortedIndex] == 0)
        {
            endOffsets[lastSortedIndex] = glyfDataLength;
        }

        var ranges = new SfntGlyphRange[numGlyphs];
        for (int index = 0; index < numGlyphs; index++)
        {
            uint offset = offsets[index];
            uint endOffset = endOffsets[index];
            ranges[index] = new SfntGlyphRange(offset, (endOffset > offset) ? endOffset - offset : 0);
        }

        return ranges;
    }
}
