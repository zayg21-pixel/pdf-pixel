using System.Numerics;

namespace PdfPixel.Geometry;

internal static partial class PdfStrokeOutlineBuilder
{
    private interface IPathSegment
    {
        Vector2 Start { get; }

        Vector2 End { get; }

        bool IsDegenerate { get; }

        IPathSegment Reversed();

        Vector2 StartTangent();

        Vector2 EndTangent();

        /// <summary>
        /// Builds an arc-length lookup backed by <paramref name="buffer"/> rather than by an allocation of
        /// its own. The caller must finish with one segment's lookup before asking for the next, since the
        /// next one overwrites the buffer. Ignored by segment kinds that don't sample a curve.
        /// </summary>
        LengthLookup CreateLengthLookup(CurveLengthBuffer buffer);

        IPathSegment Slice(float t0, float t1);

        void EmitOffset(PdfPathBuilder result, float halfWidth);
    }

    /// <summary>
    /// Reusable backing storage for a curve's arc-length table.
    /// </summary>
    private sealed class CurveLengthBuffer
    {
        public float[] Distances { get; } = new float[MaxCurveMeasureSamples];

        public float[] Parameters { get; } = new float[MaxCurveMeasureSamples];
    }

    /// <summary>
    /// Maps arc length to the parametric <c>t</c> of the segment it was built from: a plain ratio for a
    /// straight segment, an interpolation within the arc-length table for a curve.
    /// </summary>
    private readonly struct LengthLookup
    {
        private readonly float[]? _distances;
        private readonly float[]? _parameters;
        private readonly int _count;

        public LengthLookup(float length)
        {
            Length = length;
            _distances = null;
            _parameters = null;
            _count = 0;
        }

        public LengthLookup(float[] distances, float[] parameters, int count)
        {
            _distances = distances;
            _parameters = parameters;
            _count = count;
            Length = distances[count - 1];
        }

        public float Length { get; }

        public float ParameterAt(float targetLength)
        {
            if (_distances == null || _parameters == null)
            {
                return (Length <= Epsilon) ? 0f : (targetLength / Length);
            }

            for (int i = 1; i < _count; i++)
            {
                if (_distances[i] >= targetLength)
                {
                    float span = _distances[i] - _distances[i - 1];
                    float localT = (span <= Epsilon) ? 0f : ((targetLength - _distances[i - 1]) / span);
                    return _parameters[i - 1] + (localT * (_parameters[i] - _parameters[i - 1]));
                }
            }

            return 1f;
        }
    }

    private readonly struct LinearPathSegment : IPathSegment
    {
        public LinearPathSegment(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;
        }

        public Vector2 Start { get; }

        public Vector2 End { get; }

        public bool IsDegenerate => Vector2.Distance(Start, End) <= Epsilon;

        public IPathSegment Reversed() => new LinearPathSegment(End, Start);

        public Vector2 StartTangent() => Direction(Start, End);

        public Vector2 EndTangent() => Direction(Start, End);

        public LengthLookup CreateLengthLookup(CurveLengthBuffer buffer) => new(Vector2.Distance(Start, End));

        public IPathSegment Slice(float t0, float t1) => new LinearPathSegment(Vector2.Lerp(Start, End, t0), Vector2.Lerp(Start, End, t1));

        public void EmitOffset(PdfPathBuilder result, float halfWidth)
        {
            Vector2 direction = Direction(Start, End);
            Vector2 end = Offset(End, direction, halfWidth);
            result.LineTo(ToPdfPoint(end));
        }
    }

    private readonly struct CubicPathSegment : IPathSegment
    {
        public CubicPathSegment(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
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
            => Vector2.Distance(Start, Control1) <= Epsilon && Vector2.Distance(Control1, Control2) <= Epsilon && Vector2.Distance(Control2, End) <= Epsilon;

        public IPathSegment Reversed() => new CubicPathSegment(End, Control2, Control1, Start);

        public Vector2 StartTangent()
        {
            Vector2 reference = (Vector2.Distance(Start, Control1) > Epsilon) ? Control1
                : (Vector2.Distance(Start, Control2) > Epsilon) ? Control2
                : End;
            return Direction(Start, reference);
        }

        public Vector2 EndTangent()
        {
            Vector2 reference = (Vector2.Distance(End, Control2) > Epsilon) ? Control2
                : (Vector2.Distance(End, Control1) > Epsilon) ? Control1
                : Start;
            return Direction(reference, End);
        }

        public LengthLookup CreateLengthLookup(CurveLengthBuffer buffer)
        {
            buffer.Distances[0] = 0f;
            buffer.Parameters[0] = 0f;
            int count = 1;
            AppendMeasureLeaves(this, 0, MaxCurveMeasureTValue, buffer.Distances, buffer.Parameters, ref count);
            return new LengthLookup(buffer.Distances, buffer.Parameters, count);
        }

        public IPathSegment Slice(float t0, float t1)
        {
            (_, CubicPathSegment tail) = SplitAt(t0);
            float remappedT1 = (t1 - t0) / (1f - t0);
            (CubicPathSegment head, _) = tail.SplitAt(remappedT1);
            return head;
        }

        public void EmitOffset(PdfPathBuilder result, float halfWidth) => EmitOffset(result, halfWidth, depth: 0);

        private void EmitOffset(PdfPathBuilder result, float halfWidth, int depth)
        {
            if (depth < MaxSubdivisionDepth && !OffsetWithinTolerance(halfWidth))
            {
                (CubicPathSegment head, CubicPathSegment tail) = SplitAt(0.5f);
                head.EmitOffset(result, halfWidth, depth + 1);
                tail.EmitOffset(result, halfWidth, depth + 1);
                return;
            }

            Vector2 startTangent = StartTangent();
            Vector2 endTangent = EndTangent();
            Vector2 control1 = Offset(Control1, startTangent, halfWidth);
            Vector2 control2 = Offset(Control2, endTangent, halfWidth);
            Vector2 end = Offset(End, endTangent, halfWidth);

            result.CubicTo(ToPdfPoint(control1), ToPdfPoint(control2), ToPdfPoint(end));
        }

        /// <summary>
        /// True when one offset cubic tracks the true offset closely enough to skip subdivision. Translating
        /// control points along the endpoint normals leaves the offset curve sagging toward its chord in the
        /// middle, so a wide turn fails here and splits until each piece is flat enough for the sag to go.
        /// </summary>
        private bool OffsetWithinTolerance(float halfWidth)
        {
            Vector2 startTangent = StartTangent();
            Vector2 endTangent = EndTangent();

            Vector2 offsetStart = Offset(Start, startTangent, halfWidth);
            Vector2 offsetControl1 = Offset(Control1, startTangent, halfWidth);
            Vector2 offsetControl2 = Offset(Control2, endTangent, halfWidth);
            Vector2 offsetEnd = Offset(End, endTangent, halfWidth);

            Vector2 approximateMidpoint = CubicMidpoint(offsetStart, offsetControl1, offsetControl2, offsetEnd);
            Vector2 midpointTangent = CubicMidpointTangent(Start, Control1, Control2, End);
            Vector2 trueOffsetMidpoint = Offset(CubicMidpoint(Start, Control1, Control2, End), midpointTangent, halfWidth);

            return Vector2.Distance(approximateMidpoint, trueOffsetMidpoint) <= StrokeOffsetTolerance;
        }

        /// <summary>
        /// Appends one arc-length table entry per subdivision leaf, each recording its cumulative chord
        /// distance and its end <c>t</c>. <paramref name="minT"/> and <paramref name="maxT"/> carry the
        /// leaf's t-range in <see cref="MaxCurveMeasureTValue"/> units, kept as integers so halving stays
        /// exact. Subdivision stops on a t-span too small to halve, a full buffer, or a straight enough curve.
        /// </summary>
        private static void AppendMeasureLeaves(in CubicPathSegment curve, int minT, int maxT, float[] distances, float[] parameters, ref int count)
        {
            bool canSubdivide = ((maxT - minT) >> 10) != 0 && count < distances.Length - 1;
            if (canSubdivide && IsTooCurvy(curve))
            {
                (CubicPathSegment head, CubicPathSegment tail) = curve.SplitAt(0.5f);
                int midT = (minT + maxT) >> 1;
                AppendMeasureLeaves(head, minT, midT, distances, parameters, ref count);
                AppendMeasureLeaves(tail, midT, maxT, distances, parameters, ref count);
                return;
            }

            distances[count] = distances[count - 1] + Vector2.Distance(curve.Start, curve.End);
            parameters[count] = maxT / (float)MaxCurveMeasureTValue;
            count++;
        }

        // Compares each control point against where it would fall on a uniformly-parameterized straight
        // chord, so this catches a near-straight cubic whose bunched control points make arc length run
        // non-linearly in t, not only a geometrically curved one.
        private static bool IsTooCurvy(in CubicPathSegment curve)
        {
            Vector2 firstChordThird = Vector2.Lerp(curve.Start, curve.End, 1f / 3f);
            Vector2 secondChordThird = Vector2.Lerp(curve.Start, curve.End, 2f / 3f);

            return MaxAxisDistance(curve.Control1, firstChordThird) > CurveMeasureTolerance
                || MaxAxisDistance(curve.Control2, secondChordThird) > CurveMeasureTolerance;
        }

        // Superseded approach, kept for reference. This sampled the cubic at a fixed number of equal steps in
        // the parameter t and summed the chord lengths between consecutive samples into a cumulative table
        // indexed uniformly in t. It is mathematically the more accurate arc-length estimate: it converges to
        // the true arc length as the sample count grows, whereas the adaptive-leaf measure above deliberately
        // reproduces Skia's coarser chord sum, which systematically underestimates the true length. We switched
        // to the Skia-matching measure only so dash boundaries land exactly where Skia places them and do not
        // drift over a long path. That parity is no longer a hard requirement, so if a rendering target does
        // not need to agree with Skia this uniform-sampling version is the simpler, more accurate choice.
        //
        // private void BuildLengthTable(float[] table)
        // {
        //     table[0] = 0f;
        //     Vector2 previous = Start;
        //     for (int i = 1; i <= LengthSampleCount; i++)
        //     {
        //         float t = i / (float)LengthSampleCount;
        //         Vector2 point = Evaluate(t);
        //         table[i] = table[i - 1] + Vector2.Distance(previous, point);
        //         previous = point;
        //     }
        // }
        //
        // private Vector2 Evaluate(float t)
        // {
        //     float u = 1f - t;
        //     return (u * u * u * Start) + (3f * u * u * t * Control1) + (3f * u * t * t * Control2) + (t * t * t * End);
        // }

        private (CubicPathSegment Head, CubicPathSegment Tail) SplitAt(float t)
        {
            Vector2 q0 = Vector2.Lerp(Start, Control1, t);
            Vector2 q1 = Vector2.Lerp(Control1, Control2, t);
            Vector2 q2 = Vector2.Lerp(Control2, End, t);
            Vector2 r0 = Vector2.Lerp(q0, q1, t);
            Vector2 r1 = Vector2.Lerp(q1, q2, t);
            Vector2 s = Vector2.Lerp(r0, r1, t);

            return (new CubicPathSegment(Start, q0, r0, s), new CubicPathSegment(s, r1, q2, End));
        }
    }
}
