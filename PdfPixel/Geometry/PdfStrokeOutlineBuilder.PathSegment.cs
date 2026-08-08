using System;
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
        public float[] Distances { get; } = new float[MaxCurveMeasureSamples + 1];

        public float[] Parameters { get; } = new float[MaxCurveMeasureSamples + 1];
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

            return new LengthLookup(buffer.Distances, buffer.Parameters, sampleCount + 1);
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

        private Vector2 Evaluate(float t)
        {
            float u = 1f - t;

            return (u * u * u * Start) + (3f * u * u * t * Control1) + (3f * u * t * t * Control2) + (t * t * t * End);
        }

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
