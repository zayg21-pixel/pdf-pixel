using System;
using System.Buffers.Binary;

namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Represents a region segment information field common to all region-type segments.
/// Defined in ITU-T T.88 Section 7.4.1.
/// </summary>
internal sealed class Jbig2RegionHeader
{
    public const int Jbig2RegionHeaderLength = 17;

    /// <summary>
    /// Region width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Region height in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Horizontal location of the region relative to the page.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Vertical location of the region relative to the page.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Combination operator used when compositing this region onto the page buffer.
    /// </summary>
    public Jbig2CombinationOperator CombinationOperator { get; set; }

    /// <summary>
    /// Parses the 17-byte region segment information field from the start of <paramref name="data"/>.
    /// </summary>
    /// <param name="segment">Segment header; supplies <see cref="Jbig2SegmentHeader.ActualRowCount"/> override for height.</param>
    /// <param name="data">Segment data starting at the region information field.</param>
    /// <returns>Parsed region header.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="data"/> is shorter than <see cref="Jbig2RegionHeaderLength"/> bytes.
    /// </exception>
    public static Jbig2RegionHeader Parse(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> data)
    {
        if (data.Length < Jbig2RegionHeaderLength)
        {
            throw new InvalidOperationException(
                $"JBIG2 region segment data too short: expected at least {Jbig2RegionHeaderLength} bytes, got {data.Length}.");
        }

        return new Jbig2RegionHeader
        {
            Width = BinaryPrimitives.ReadInt32BigEndian(data.Slice(0, 4)),
            Height = (segment.ActualRowCount.HasValue)
                ? (int)segment.ActualRowCount.Value
                : BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4)),
            X = BinaryPrimitives.ReadInt32BigEndian(data.Slice(8, 4)),
            Y = BinaryPrimitives.ReadInt32BigEndian(data.Slice(12, 4)),
            CombinationOperator = (Jbig2CombinationOperator)(data[16] & 0x07)
        };
    }
}
