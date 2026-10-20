using System;
using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// A cubic Bézier segment of a <see cref="PdfContourMeasure"/>.
/// </summary>
internal readonly struct PdfCubicContourSegment : IPdfContourSegment
{
    // Maximum subdivision levels for one source curve's offset rail.
    public const int MaxSubdivisionDepth = 6;

    // Spacing of the chord samples a cubic's length is measured from, in the space the path is built in.
    public const float CurveMeasureStep = 0.25f;
    public const int MinCurveMeasureSamples = 8;
    public const int MaxCurveMeasureSamples = 256;

    public PdfCubicContourSegment(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        Start = start;
        Control1 = control1;
        Control2 = control2;
        End = end;
    }

    public Vector2 Start { get; }

    public Vector2 Control1 { get; }

    public Vector2 Control2 { get; }

    public Vector2 End { get; }

    public bool IsDegenerate
        => Vector2.Distance(Start, Control1) <= PdfVectorMath.Epsilon && Vector2.Distance(Control1, Control2) <= PdfVectorMath.Epsilon && Vector2.Distance(Control2, End) <= PdfVectorMath.Epsilon;

    public Vector2 StartTangent()
    {
        Vector2 reference = (Vector2.Distance(Start, Control1) > PdfVectorMath.Epsilon) ? Control1
            : (Vector2.Distance(Start, Control2) > PdfVectorMath.Epsilon) ? Control2
            : End;
        return PdfVectorMath.Direction(Start, reference);
    }

    public Vector2 EndTangent()
    {
        Vector2 reference = (Vector2.Distance(End, Control2) > PdfVectorMath.Epsilon) ? Control2
            : (Vector2.Distance(End, Control1) > PdfVectorMath.Epsilon) ? Control1
            : Start;
        return PdfVectorMath.Direction(reference, End);
    }

    /// <inheritdoc />
    public Vector2 Evaluate(float t)
    {
        float u = 1f - t;

        return (u * u * u * Start) + (3f * u * u * t * Control1) + (3f * u * t * t * Control2) + (t * t * t * End);
    }

    /// <inheritdoc />
    public void AppendTo(PdfPathBuilder builder)
        => builder.CubicTo(PdfVectorMath.ToPdfPoint(Control1), PdfVectorMath.ToPdfPoint(Control2), PdfVectorMath.ToPdfPoint(End));

    public PdfLengthLookup CreateLengthLookup(PdfCurveLengthBuffer buffer)
    {
        int sampleCount = GetSampleCount();

        buffer.Distances[0] = 0f;
        buffer.Parameters[0] = 0f;

        Vector2 previous = Start;

        for (int index = 1; index <= sampleCount; index++)
        {
            float parameter = index / (float)sampleCount;
            Vector2 point = Evaluate(parameter);

            buffer.Distances[index] = buffer.Distances[index - 1] + Vector2.Distance(previous, point);
            buffer.Parameters[index] = parameter;
            previous = point;
        }

        return new PdfLengthLookup(buffer.Distances, buffer.Parameters, sampleCount + 1);
    }

    public IPdfContourSegment Slice(float t0, float t1)
    {
        PdfCubicSplit startSplit = SplitAt(t0);
        float remappedT1 = (t1 - t0) / (1f - t0);
        PdfCubicSplit endSplit = startSplit.Tail.SplitAt(remappedT1);
        return endSplit.Head;
    }

    /// <inheritdoc />
    public IPdfContourSegment Transform(in PdfMatrix matrix)
        => new PdfCubicContourSegment(matrix.MapVector2(Start), matrix.MapVector2(Control1), matrix.MapVector2(Control2), matrix.MapVector2(End));

    /// <inheritdoc />
    public void EmitOffset(PdfPathBuilder result, float halfWidth, float offsetTolerance, Vector2 startTangent, Vector2 endTangent, bool reversed)
    {
        PdfCubicContourSegment walked = reversed ? new PdfCubicContourSegment(End, Control2, Control1, Start) : this;
        EmitOffset(walked, result, halfWidth, offsetTolerance, startTangent, endTangent, depth: 0);
    }

    /// <summary>
    /// Emits <paramref name="segment"/>'s offset, splitting it until each piece stays within tolerance.
    /// <paramref name="startTangent"/> and <paramref name="endTangent"/> are the curve's unit end
    /// tangents; the halves of a split take them and the midpoint tangent they meet at. Only the near
    /// half of a split descends — the far half continues in this call, since it is what remains of the
    /// curve once the near half has been emitted.
    /// </summary>
    private static void EmitOffset(in PdfCubicContourSegment segment, PdfPathBuilder result, float halfWidth, float offsetTolerance, Vector2 startTangent, Vector2 endTangent, int depth)
    {
        PdfCubicContourSegment current = segment;

        while (depth < MaxSubdivisionDepth && !current.OffsetWithinTolerance(halfWidth, offsetTolerance, startTangent, endTangent, out Vector2 midpointTangent))
        {
            PdfCubicSplit split = current.SplitAtMidpoint();

            EmitOffset(split.Head, result, halfWidth, offsetTolerance, startTangent, midpointTangent, depth + 1);

            current = split.Tail;
            startTangent = midpointTangent;
            depth++;
        }

        Vector2 control1 = PdfVectorMath.Offset(current.Control1, startTangent, halfWidth);
        Vector2 control2 = PdfVectorMath.Offset(current.Control2, endTangent, halfWidth);
        Vector2 end = PdfVectorMath.Offset(current.End, endTangent, halfWidth);

        result.CubicTo(PdfVectorMath.ToPdfPoint(control1), PdfVectorMath.ToPdfPoint(control2), PdfVectorMath.ToPdfPoint(end));
    }

    /// <summary>
    /// True when one offset cubic tracks the true offset closely enough to skip subdivision. Translating
    /// control points along the endpoint normals leaves the offset curve sagging toward its chord in the
    /// middle, so a wide turn fails here and splits until each piece is flat enough for the sag to go.
    /// </summary>
    private bool OffsetWithinTolerance(float halfWidth, float offsetTolerance, Vector2 startTangent, Vector2 endTangent, out Vector2 midpointTangent)
    {
        midpointTangent = PdfVectorMath.CubicMidpointTangent(Start, Control1, Control2, End);
        Vector2 midpointSag = ((startTangent + endTangent) * 0.5f) - midpointTangent;

        return (halfWidth * halfWidth * midpointSag.LengthSquared()) <= (offsetTolerance * offsetTolerance);
    }

    /// <summary>
    /// How many even steps in <c>t</c> the curve is measured over. The control polygon is never shorter
    /// than the curve, so scaling the count by it keeps the samples no further apart than
    /// <see cref="CurveMeasureStep"/> however long or short the curve turns out to be.
    /// </summary>
    private int GetSampleCount()
    {
        float polygonLength = Vector2.Distance(Start, Control1)
            + Vector2.Distance(Control1, Control2)
            + Vector2.Distance(Control2, End);

        var sampleCount = (int)MathF.Ceiling(polygonLength / CurveMeasureStep);

        return Math.Min(MaxCurveMeasureSamples, Math.Max(MinCurveMeasureSamples, sampleCount));
    }

    private PdfCubicSplit SplitAt(float t)
    {
        Vector2 q0 = Vector2.Lerp(Start, Control1, t);
        Vector2 q1 = Vector2.Lerp(Control1, Control2, t);
        Vector2 q2 = Vector2.Lerp(Control2, End, t);
        Vector2 r0 = Vector2.Lerp(q0, q1, t);
        Vector2 r1 = Vector2.Lerp(q1, q2, t);
        Vector2 s = Vector2.Lerp(r0, r1, t);

        return new PdfCubicSplit(new PdfCubicContourSegment(Start, q0, r0, s), new PdfCubicContourSegment(s, r1, q2, End));
    }

    /// <summary>
    /// The two halves this curve meets itself at, which is the split the offset subdivision always
    /// takes. Every de Casteljau step is a plain midpoint here, so none of them weigh their ends.
    /// </summary>
    private PdfCubicSplit SplitAtMidpoint()
    {
        Vector2 q0 = (Start + Control1) * 0.5f;
        Vector2 q1 = (Control1 + Control2) * 0.5f;
        Vector2 q2 = (Control2 + End) * 0.5f;
        Vector2 r0 = (q0 + q1) * 0.5f;
        Vector2 r1 = (q1 + q2) * 0.5f;
        Vector2 midpoint = (r0 + r1) * 0.5f;

        return new PdfCubicSplit(new PdfCubicContourSegment(Start, q0, r0, midpoint), new PdfCubicContourSegment(midpoint, r1, q2, End));
    }
}
