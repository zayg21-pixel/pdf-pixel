using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a single subtable within a font's "cmap" table. Its mapping is parsed lazily: the
/// directory only records its format and byte offset - <see cref="SfntCmapProcessor.GetGid"/> parses
/// it into a sorted set of <see cref="ISfntCmapRange"/>s on first use and caches the result here, so a
/// subtable nothing ever queries (e.g. a font's 12th cmap subtable when the 1st already answered
/// everything) is never parsed at all.
/// </summary>
public class SfntCmapSubtable
{
    private ISfntCmapRange[]? _ranges;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntCmapSubtable"/> class.
    /// </summary>
    /// <param name="format">The subtable format number (e.g. 0, 4, 6) as defined by the OpenType spec.</param>
    /// <param name="platformId">The subtable record's platform ID.</param>
    /// <param name="encodingId">The subtable record's platform-specific encoding ID.</param>
    /// <param name="encoding">The PDF font encoding implied by <paramref name="platformId"/>/<paramref name="encodingId"/>, or null if unrecognized.</param>
    /// <param name="subtableOffset">This subtable's byte offset within the "cmap" table.</param>
    public SfntCmapSubtable(ushort format, ushort platformId, ushort encodingId, PdfFontEncoding? encoding, int subtableOffset)
    {
        Format = format;
        PlatformId = platformId;
        EncodingId = encodingId;
        Encoding = encoding;
        SubtableOffset = subtableOffset;
    }

    /// <summary>
    /// Gets the subtable format number (e.g. 0, 4, 6) as defined by the OpenType spec.
    /// </summary>
    public ushort Format { get; }

    /// <summary>
    /// Gets the subtable record's platform ID.
    /// </summary>
    public ushort PlatformId { get; }

    /// <summary>
    /// Gets the subtable record's platform-specific encoding ID.
    /// </summary>
    public ushort EncodingId { get; }

    /// <summary>
    /// Gets the PDF font encoding implied by <see cref="PlatformId"/>/<see cref="EncodingId"/>, or null
    /// if unrecognized.
    /// </summary>
    public PdfFontEncoding? Encoding { get; }

    /// <summary>
    /// Gets this subtable's byte offset within the "cmap" table, for <see cref="SfntCmapProcessor.GetGid"/>
    /// to parse it from on first use.
    /// </summary>
    internal int SubtableOffset { get; }

    /// <summary>
    /// Gets whether this subtable's ranges have already been parsed.
    /// </summary>
    internal bool IsResolved => _ranges != null;

    /// <summary>
    /// Caches this subtable's parsed ranges, sorted by <see cref="ISfntCmapRange.StartCode"/>.
    /// </summary>
    internal void SetRanges(ISfntCmapRange[] ranges)
    {
        Array.Sort(ranges, (left, right) => left.StartCode.CompareTo(right.StartCode));
        _ranges = ranges;
    }

    /// <summary>
    /// Resolves a character code to a glyph id via binary search over this subtable's ranges, or null
    /// if no range covers <paramref name="code"/> or the covering range has no mapping for it. Only
    /// meaningful once <see cref="IsResolved"/> is <see langword="true"/> - use
    /// <see cref="SfntCmapProcessor.GetGid"/> instead of calling this directly.
    /// </summary>
    internal ushort? GetGid(int code)
    {
        if (_ranges == null)
        {
            return null;
        }

        int low = 0;
        int high = _ranges.Length - 1;

        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            ISfntCmapRange range = _ranges[mid];

            if (code < range.StartCode)
            {
                high = mid - 1;
            }
            else if (code > range.EndCode)
            {
                low = mid + 1;
            }
            else
            {
                return range.GetGid(code);
            }
        }

        return null;
    }
}
