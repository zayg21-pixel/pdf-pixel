using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Writes a collected glyph outline back out as a simple "glyf" glyph. A composite glyph's components
/// have already been flattened into the outline by then, so the result never refers to another glyph.
/// The bounding box is recomputed from the points, and every delta is encoded in the shortest form
/// that holds it - the outline records only which points lie on the curve, not how the source chose
/// to store its coordinates.
/// </summary>
public static class SfntGlyphRepacker
{
    private const int ShortDeltaLimit = 255;
    private const int MaxRepeatRunLength = 256;

    /// <summary>
    /// Repacks one glyph. Returns an empty glyph when the outline has no points, which "loca" records
    /// as a glyph without an outline.
    /// </summary>
    /// <param name="outline">The glyph's collected outline.</param>
    public static byte[] Repack(GlyphOutline outline)
    {
        if (outline == null)
        {
            throw new ArgumentNullException(nameof(outline));
        }

        ReadOnlySpan<PdfFontPoint> points = outline.Points.Span;
        int pointCount = points.Length;

        if (pointCount == 0 || outline.EndPoints.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var xDeltas = new int[pointCount];
        var yDeltas = new int[pointCount];
        var flags = new byte[pointCount];

        short xMin = short.MaxValue;
        short yMin = short.MaxValue;
        short xMax = short.MinValue;
        short yMax = short.MinValue;

        short previousX = 0;
        short previousY = 0;

        ReadOnlySpan<byte> outlineFlags = outline.Flags.Span;

        for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            PdfFontPoint point = points[pointIndex];
            short roundedX = ToFontUnits(point.X);
            short roundedY = ToFontUnits(point.Y);

            xMin = Math.Min(xMin, roundedX);
            yMin = Math.Min(yMin, roundedY);
            xMax = Math.Max(xMax, roundedX);
            yMax = Math.Max(yMax, roundedY);

            xDeltas[pointIndex] = roundedX - previousX;
            yDeltas[pointIndex] = roundedY - previousY;
            previousX = roundedX;
            previousY = roundedY;

            var onCurve = (byte)(outlineFlags[pointIndex] & SfntGlyphFlags.OnCurve);
            flags[pointIndex] = (byte)(onCurve
                | EncodeDeltaFlag(xDeltas[pointIndex], SfntGlyphFlags.XShort, SfntGlyphFlags.XSame)
                | EncodeDeltaFlag(yDeltas[pointIndex], SfntGlyphFlags.YShort, SfntGlyphFlags.YSame));
        }

        SfntWriter writer = new();
        writer.WriteInt16((short)outline.EndPoints.Length);
        writer.WriteInt16(xMin);
        writer.WriteInt16(yMin);
        writer.WriteInt16(xMax);
        writer.WriteInt16(yMax);

        foreach (int endPoint in outline.EndPoints.Span)
        {
            writer.WriteUInt16((ushort)endPoint);
        }

        // The points below keep the numbers they had, so whatever the instructions addressed they
        // still address; a flattened composite carries none, its components having been renumbered.
        ReadOnlySpan<byte> instructions = outline.Instructions.Span;
        writer.WriteUInt16((ushort)instructions.Length);
        writer.WriteBytes(instructions);

        WriteFlags(writer, flags);
        WriteDeltas(writer, flags, xDeltas, SfntGlyphFlags.XShort);
        WriteDeltas(writer, flags, yDeltas, SfntGlyphFlags.YShort);

        return writer.Detach();
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
    private static void WriteFlags(SfntWriter writer, byte[] flags)
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
    private static void WriteDeltas(SfntWriter writer, byte[] flags, int[] deltas, byte shortFlag)
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

    private static short ToFontUnits(float value) => ClampToFontUnits((int)Math.Round(value, MidpointRounding.AwayFromZero));

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
}
