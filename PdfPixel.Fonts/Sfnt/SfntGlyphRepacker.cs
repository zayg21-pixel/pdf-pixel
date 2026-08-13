using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Writes a simple glyph's outline back out as a simple "glyf" glyph. The bounding box is recomputed
/// from the points, and every delta is encoded in the shortest form that holds it - the outline
/// records only which points lie on the curve, not how the source chose to store its coordinates.
/// The points arrive in the design units the format stores and are written as they are: nothing on
/// this path places, scales or rounds them.
/// </summary>
/// <remarks>
/// One instance repacks a whole table: the per-point scratch buffers grow to the largest glyph seen
/// and are reused by every glyph after it, so a table costs one set of buffers rather than one per
/// glyph.
/// </remarks>
internal sealed class SfntGlyphRepacker
{
    private const int ShortDeltaLimit = 255;
    private const int MaxRepeatRunLength = 256;

    private int[] _xDeltas = [];
    private int[] _yDeltas = [];
    private byte[] _flags = [];

    /// <summary>
    /// Repacks one glyph, appending it to <paramref name="writer"/>. Writes nothing when the outline
    /// has no points, which "loca" records as a glyph without an outline.
    /// </summary>
    /// <param name="writer">The writer the glyph's bytes are appended to.</param>
    /// <param name="outline">The glyph's collected outline.</param>
    public void Repack(SfntWriter writer, GlyphOutline outline)
    {
        if (outline == null)
        {
            throw new ArgumentNullException(nameof(outline));
        }

        ReadOnlySpan<GlyphPoint> points = outline.Points;
        ReadOnlySpan<int> endPoints = outline.EndPoints;
        int pointCount = points.Length;

        if (pointCount == 0 || endPoints.Length == 0)
        {
            return;
        }

        EnsureCapacity(pointCount);

        Span<int> xDeltas = _xDeltas.AsSpan(0, pointCount);
        Span<int> yDeltas = _yDeltas.AsSpan(0, pointCount);
        Span<byte> flags = _flags.AsSpan(0, pointCount);

        short xMin = short.MaxValue;
        short yMin = short.MaxValue;
        short xMax = short.MinValue;
        short yMax = short.MinValue;

        short previousX = 0;
        short previousY = 0;

        ReadOnlySpan<byte> outlineFlags = outline.Flags;

        for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            GlyphPoint point = points[pointIndex];
            short x = point.X;
            short y = point.Y;

            xMin = Math.Min(xMin, x);
            yMin = Math.Min(yMin, y);
            xMax = Math.Max(xMax, x);
            yMax = Math.Max(yMax, y);

            int xDelta = x - previousX;
            int yDelta = y - previousY;
            xDeltas[pointIndex] = xDelta;
            yDeltas[pointIndex] = yDelta;
            previousX = x;
            previousY = y;

            var onCurve = (byte)(outlineFlags[pointIndex] & SfntGlyphFlags.OnCurve);
            flags[pointIndex] = (byte)(onCurve
                | EncodeDeltaFlag(xDelta, SfntGlyphFlags.XShort, SfntGlyphFlags.XSame)
                | EncodeDeltaFlag(yDelta, SfntGlyphFlags.YShort, SfntGlyphFlags.YSame));
        }

        writer.WriteInt16((short)endPoints.Length);
        writer.WriteInt16(xMin);
        writer.WriteInt16(yMin);
        writer.WriteInt16(xMax);
        writer.WriteInt16(yMax);

        foreach (int endPoint in endPoints)
        {
            writer.WriteUInt16((ushort)endPoint);
        }

        // Write no instructions, stack based hints disabled
        writer.WriteUInt16(0);

        WriteFlags(writer, flags);
        WriteDeltas(writer, flags, xDeltas, SfntGlyphFlags.XShort);
        WriteDeltas(writer, flags, yDeltas, SfntGlyphFlags.YShort);
    }

    /// <summary>
    /// Reports the flag bits describing how <paramref name="delta"/> is stored: nothing at all when it
    /// fits in neither short form, the short flag plus the sign the same-flag doubles as when it fits
    /// in a byte, and the same-flag alone when the coordinate does not move.
    /// </summary>
    private static byte EncodeDeltaFlag(int delta, byte shortFlag, byte sameFlag)
    {
        if (delta == 0)
        {
            return sameFlag;
        }

        if (delta >= -ShortDeltaLimit && delta <= ShortDeltaLimit)
        {
            return (byte)(shortFlag | ((delta > 0) ? sameFlag : 0));
        }

        return 0;
    }

    /// <summary>
    /// Writes the per-point flags, collapsing a run of identical ones into a single flag byte carrying
    /// <see cref="SfntGlyphFlags.Repeat"/> and the count of the points that repeat it.
    /// </summary>
    private static void WriteFlags(SfntWriter writer, in ReadOnlySpan<byte> flags)
    {
        for (int pointIndex = 0; pointIndex < flags.Length;)
        {
            byte flag = flags[pointIndex];

            int runLength = 1;
            while (pointIndex + runLength < flags.Length
                && flags[pointIndex + runLength] == flag
                && runLength < MaxRepeatRunLength)
            {
                runLength++;
            }

            if (runLength > 1)
            {
                writer.WriteByte((byte)(flag | SfntGlyphFlags.Repeat));
                writer.WriteByte((byte)(runLength - 1));
            }
            else
            {
                writer.WriteByte(flag);
            }

            pointIndex += runLength;
        }
    }

    /// <summary>
    /// Writes one axis' deltas in the forms the flags written alongside them declare.
    /// </summary>
    private static void WriteDeltas(SfntWriter writer, in ReadOnlySpan<byte> flags, in ReadOnlySpan<int> deltas, byte shortFlag)
    {
        for (int pointIndex = 0; pointIndex < deltas.Length; pointIndex++)
        {
            int delta = deltas[pointIndex];

            if ((flags[pointIndex] & shortFlag) != 0)
            {
                writer.WriteByte((byte)Math.Abs(delta));
            }
            else if (delta != 0)
            {
                writer.WriteInt16(ClampToFontUnits(delta));
            }
        }
    }

    private static short ClampToFontUnits(int value)
    {
        if (value < short.MinValue)
        {
            return short.MinValue;
        }

        if (value > short.MaxValue)
        {
            return short.MaxValue;
        }

        return (short)value;
    }

    /// <summary>
    /// Grows the per-point scratch buffers to hold <paramref name="pointCount"/> points, at least
    /// doubling them so a table whose glyphs grow steadily does not reallocate on every one.
    /// </summary>
    private void EnsureCapacity(int pointCount)
    {
        if (pointCount <= _flags.Length)
        {
            return;
        }

        int newCapacity = _flags.Length * 2;
        if (newCapacity < pointCount)
        {
            newCapacity = pointCount;
        }

        _xDeltas = new int[newCapacity];
        _yDeltas = new int[newCapacity];
        _flags = new byte[newCapacity];
    }
}
