using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Writes the binary form of the SFNT "cmap" table from a character-code-to-glyph mapping.
/// </summary>
public static class SfntCmapGenerator
{
    private const int HeaderLength = 4;
    private const int RecordLength = 8;

    private const ushort TerminatorCode = 0xFFFF;
    private const int Format4FixedLength = 14;

    /// <summary>
    /// Writes a "cmap" table holding one format 4 subtable for the given platform and encoding, stating
    /// <paramref name="codeToGid"/>. Codes outside the basic multilingual plane, and the 0xFFFF format 4
    /// reserves for its terminating segment, are left out of the result.
    /// </summary>
    /// <param name="platformId">The subtable's platform: 3 for Windows, 1 for Macintosh.</param>
    /// <param name="encodingId">The subtable's encoding within that platform: for Windows, 1 for Unicode BMP and 0 for Symbol.</param>
    /// <param name="codeToGid">The character code to glyph id mapping the subtable states.</param>
    public static byte[] Write(ushort platformId, ushort encodingId, IReadOnlyDictionary<int, ushort> codeToGid)
    {
        if (codeToGid == null)
        {
            throw new ArgumentNullException(nameof(codeToGid));
        }

        List<int> codes = new(codeToGid.Count);
        foreach (KeyValuePair<int, ushort> entry in codeToGid)
        {
            if (entry.Key >= 0 && entry.Key < TerminatorCode)
            {
                codes.Add(entry.Key);
            }
        }

        codes.Sort();

        List<Format4Segment> segments = BuildSegments(codes, codeToGid);
        List<ushort> glyphIdArray = [];

        foreach (Format4Segment segment in segments)
        {
            if (segment.GlyphIds != null)
            {
                glyphIdArray.AddRange(segment.GlyphIds);
            }
        }

        int segCount = segments.Count;
        int segmentBlockLength = (segCount * 8) + 2; // endCode[] + reservedPad + startCode[] + idDelta[] + idRangeOffset[]
        int subtableLength = Format4FixedLength + segmentBlockLength + (glyphIdArray.Count * 2);
        int subtableOffset = HeaderLength + RecordLength;

        SfntWriter writer = new(subtableOffset + subtableLength);

        writer.WriteUInt16(0); // version
        writer.WriteUInt16(1); // numTables
        writer.WriteUInt16(platformId);
        writer.WriteUInt16(encodingId);
        writer.WriteUInt32((uint)subtableOffset);

        ushort searchRange = GetSearchRange(segCount);

        writer.WriteUInt16(4); // format
        writer.WriteUInt16((ushort)subtableLength);
        writer.WriteUInt16(0); // language
        writer.WriteUInt16((ushort)(segCount * 2));
        writer.WriteUInt16(searchRange);
        writer.WriteUInt16(GetEntrySelector(searchRange));
        writer.WriteUInt16((ushort)((segCount * 2) - searchRange));

        foreach (Format4Segment segment in segments)
        {
            writer.WriteUInt16(segment.EndCode);
        }

        writer.WriteUInt16(0); // reservedPad

        foreach (Format4Segment segment in segments)
        {
            writer.WriteUInt16(segment.StartCode);
        }

        foreach (Format4Segment segment in segments)
        {
            writer.WriteInt16(segment.IdDelta);
        }

        int glyphIdArrayIndex = 0;
        for (int segmentIndex = 0; segmentIndex < segCount; segmentIndex++)
        {
            ushort[]? glyphIds = segments[segmentIndex].GlyphIds;
            if (glyphIds == null)
            {
                writer.WriteUInt16(0);
                continue;
            }

            // The offset counts from this very entry, past the idRangeOffset entries that follow it,
            // to where this segment's run starts within the glyph id array.
            writer.WriteUInt16((ushort)((glyphIdArrayIndex - segmentIndex + segCount) * 2));
            glyphIdArrayIndex += glyphIds.Length;
        }

        foreach (ushort gid in glyphIdArray)
        {
            writer.WriteUInt16(gid);
        }

        return writer.Detach();
    }

    /// <summary>
    /// Splits the sorted codes into one segment per run of consecutive codes, closing with the
    /// terminating 0xFFFF segment.
    /// </summary>
    private static List<Format4Segment> BuildSegments(List<int> codes, IReadOnlyDictionary<int, ushort> codeToGid)
    {
        List<Format4Segment> segments = [];

        int runStart = 0;
        while (runStart < codes.Count)
        {
            int runEnd = runStart;
            while (runEnd + 1 < codes.Count && codes[runEnd + 1] == codes[runEnd] + 1)
            {
                runEnd++;
            }

            segments.Add(BuildSegment(codes, codeToGid, runStart, runEnd));
            runStart = runEnd + 1;
        }

        segments.Add(new Format4Segment(TerminatorCode, TerminatorCode, idDelta: 1, glyphIds: null));

        return segments;
    }

    /// <summary>
    /// Builds the segment covering the run of codes from <paramref name="runStart"/> to
    /// <paramref name="runEnd"/>. A run whose glyph ids ascend in step with its codes is stated as a
    /// single delta; any other run carries an explicit glyph id per code.
    /// </summary>
    private static Format4Segment BuildSegment(List<int> codes, IReadOnlyDictionary<int, ushort> codeToGid, int runStart, int runEnd)
    {
        var startCode = (ushort)codes[runStart];
        var endCode = (ushort)codes[runEnd];
        ushort firstGid = codeToGid[codes[runStart]];

        var isSequential = true;
        for (int index = runStart; index <= runEnd; index++)
        {
            if (codeToGid[codes[index]] != (ushort)(firstGid + index - runStart))
            {
                isSequential = false;
                break;
            }
        }

        if (isSequential)
        {
            return new Format4Segment(startCode, endCode, (short)(firstGid - startCode), glyphIds: null);
        }

        var glyphIds = new ushort[runEnd - runStart + 1];
        for (int index = runStart; index <= runEnd; index++)
        {
            glyphIds[index - runStart] = codeToGid[codes[index]];
        }

        return new Format4Segment(startCode, endCode, idDelta: 0, glyphIds);
    }

    /// <summary>
    /// Twice the largest power of two that is at most <paramref name="segCount"/>.
    /// </summary>
    private static ushort GetSearchRange(int segCount)
    {
        int highestPowerOfTwo = 1;
        while (highestPowerOfTwo * 2 <= segCount)
        {
            highestPowerOfTwo *= 2;
        }

        return (ushort)(highestPowerOfTwo * 2);
    }

    /// <summary>
    /// The base-2 logarithm of half <paramref name="searchRange"/>.
    /// </summary>
    private static ushort GetEntrySelector(ushort searchRange)
    {
        ushort entrySelector = 0;

        for (int value = searchRange / 2; value > 1; value /= 2)
        {
            entrySelector++;
        }

        return entrySelector;
    }

    /// <summary>
    /// One format 4 segment: a run of consecutive character codes, whose glyph ids come either from
    /// <see cref="IdDelta"/> alone or from an explicit <see cref="GlyphIds"/> array.
    /// </summary>
    private readonly struct Format4Segment
    {
        public Format4Segment(ushort startCode, ushort endCode, short idDelta, ushort[]? glyphIds)
        {
            StartCode = startCode;
            EndCode = endCode;
            IdDelta = idDelta;
            GlyphIds = glyphIds;
        }

        public ushort StartCode { get; }

        public ushort EndCode { get; }

        public short IdDelta { get; }

        /// <summary>
        /// This segment's glyph id per code, or null when <see cref="IdDelta"/> states them all.
        /// </summary>
        public ushort[]? GlyphIds { get; }
    }
}
