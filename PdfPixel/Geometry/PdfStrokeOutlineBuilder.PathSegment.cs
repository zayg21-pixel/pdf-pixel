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
        /// Builds an arc-length lookup usable for the lifetime of this segment value, using
        /// <paramref name="tableBuffer"/> (length <see cref="LengthSampleCount"/> + 1) as curve sample storage
        /// instead of allocating a new array — the caller owns one buffer and reuses it across segments,
        /// processing one segment's lookup fully before requesting the next. Ignored by segment kinds that
        /// don't sample a curve.
        /// </summary>
        LengthLookup CreateLengthLookup(float[] tableBuffer);

        IPathSegment Slice(float t0, float t1);

        void EmitOffset(PdfPathBuilder result, float halfWidth);
    }

    /// <summary>
    /// Maps between arc length and the parametric <c>t</c> of the segment it was built from. For a straight
    /// segment this is a plain ratio; for a curve it interpolates within a precomputed arc-length table,
    /// built once by <see cref="IPathSegment.CreateLengthLookup"/> and shared across every query against
    /// that segment.
    /// </summary>
    private readonly struct LengthLookup
    {
        private readonly float[]? _table;

        public LengthLookup(float length, float[]? table)
        {
            Length = length;
            _table = table;
        }

        public float Length { get; }

        public float ParameterAt(float targetLength)
        {
            if (_table == null)
            {
                return (Length <= Epsilon) ? 0f : (targetLength / Length);
            }

            int sampleCount = _table.Length - 1;
            for (int i = 0; i < sampleCount; i++)
            {
                if (_table[i + 1] >= targetLength)
                {
                    float sampleSpan = _table[i + 1] - _table[i];
                    float localT = (sampleSpan <= Epsilon) ? 0f : ((targetLength - _table[i]) / sampleSpan);
                    return (i + localT) / sampleCount;
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

        public bool IsDegenerate => Distance(Start, End) <= Epsilon;

        public IPathSegment Reversed() => new LinearPathSegment(End, Start);

        public Vector2 StartTangent() => Direction(Start, End);

        public Vector2 EndTangent() => Direction(Start, End);

        public LengthLookup CreateLengthLookup(float[] tableBuffer) => new(Distance(Start, End), table: null);

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
            => Distance(Start, Control1) <= Epsilon && Distance(Control1, Control2) <= Epsilon && Distance(Control2, End) <= Epsilon;

        public IPathSegment Reversed() => new CubicPathSegment(End, Control2, Control1, Start);

        public Vector2 StartTangent()
        {
            Vector2 reference = (Distance(Start, Control1) > Epsilon) ? Control1
                : (Distance(Start, Control2) > Epsilon) ? Control2
                : End;
            return Direction(Start, reference);
        }

        public Vector2 EndTangent()
        {
            Vector2 reference = (Distance(End, Control2) > Epsilon) ? Control2
                : (Distance(End, Control1) > Epsilon) ? Control1
                : Start;
            return Direction(reference, End);
        }

        public LengthLookup CreateLengthLookup(float[] tableBuffer)
        {
            BuildLengthTable(tableBuffer);
            return new LengthLookup(tableBuffer[tableBuffer.Length - 1], tableBuffer);
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
            Vector2 startTangent = StartTangent();
            Vector2 endTangent = EndTangent();

            float turn = MathF.Abs(AngleDegrees(startTangent, endTangent));

            if (turn > MaxTurnDegreesPerCubicPiece && depth < MaxSubdivisionDepth)
            {
                (CubicPathSegment head, CubicPathSegment tail) = SplitAt(0.5f);
                head.EmitOffset(result, halfWidth, depth + 1);
                tail.EmitOffset(result, halfWidth, depth + 1);
                return;
            }

            Vector2 control1 = Offset(Control1, startTangent, halfWidth);
            Vector2 control2 = Offset(Control2, endTangent, halfWidth);
            Vector2 end = Offset(End, endTangent, halfWidth);

            result.CubicTo(ToPdfPoint(control1), ToPdfPoint(control2), ToPdfPoint(end));
        }

        /// <summary>
        /// Fills <paramref name="table"/> (length <see cref="LengthSampleCount"/> + 1, caller-owned) with a
        /// cumulative-arc-length lookup sampled at <see cref="LengthSampleCount"/> equal steps in the curve's
        /// parameter <c>t</c>. <c>table[i]</c> is the arc length from the segment's start to
        /// <c>t = i / LengthSampleCount</c>. Used to convert a target arc-length position into the correct
        /// parametric <c>t</c>, since arc length is not linear in <c>t</c> for a curve.
        /// </summary>
        private void BuildLengthTable(float[] table)
        {
            table[0] = 0f;
            Vector2 previous = Start;

            for (int i = 1; i <= LengthSampleCount; i++)
            {
                float t = i / (float)LengthSampleCount;
                Vector2 point = Evaluate(t);
                table[i] = table[i - 1] + Distance(previous, point);
                previous = point;
            }
        }

        private Vector2 Evaluate(float t)
        {
            float u = 1f - t;
            float a = u * u * u;
            float b = 3f * u * u * t;
            float c = 3f * u * t * t;
            float d = t * t * t;

            return (a * Start) + (b * Control1) + (c * Control2) + (d * End);
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
