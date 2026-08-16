using PdfPixel.Color.Paint;
using System;
using System.Numerics;

namespace PdfPixel.Geometry;

public static partial class PdfStrokeOutlineBuilder
{
    private static void AddJoin(
        PdfPathBuilder result,
        Vector2 vertex,
        Vector2 incomingTangent,
        Vector2 outgoingTangent,
        float halfWidth,
        PdfStrokeJoin join,
        float miterLimit)
    {
        float sweep = AngleDegrees(incomingTangent, outgoingTangent);
        if (MathF.Abs(sweep) <= Epsilon)
        {
            return;
        }

        if (sweep > 0f)
        {
            // Concave side: route the inner contour back through the vertex before stepping out to the
            // outgoing offset. Anchoring the overlap at the centerline keeps the winding number consistent
            // when a leg is shorter than the stroke radius; a direct line to the offset point instead leaves
            // a reversed-winding sliver that cancels and punches a hole in the fill (the short-leg problem).
            result.LineTo(ToPdfPoint(vertex));
            Vector2 concaveEnd = Offset(vertex, outgoingTangent, halfWidth);
            result.LineTo(ToPdfPoint(concaveEnd));
            return;
        }

        switch (join)
        {
            case PdfStrokeJoin.Round:
            {
                AddArc(result, vertex, halfWidth, AngleDegreesOf(LeftNormal(incomingTangent, 1f)), sweep);
                break;
            }
            case PdfStrokeJoin.Bevel:
            {
                Vector2 end = Offset(vertex, outgoingTangent, halfWidth);
                result.LineTo(ToPdfPoint(end));
                break;
            }
            default:
            {
                AddMiterJoin(result, vertex, incomingTangent, outgoingTangent, halfWidth, miterLimit);
                break;
            }
        }
    }

    private static void AddMiterJoin(
        PdfPathBuilder result,
        Vector2 vertex,
        Vector2 incomingTangent,
        Vector2 outgoingTangent,
        float halfWidth,
        float miterLimit)
    {
        Vector2 start = Offset(vertex, incomingTangent, halfWidth);
        Vector2 end = Offset(vertex, outgoingTangent, halfWidth);

        if (TryIntersectLines(start, incomingTangent, end, outgoingTangent, out Vector2 miterPoint))
        {
            float miterLength = Vector2.Distance(miterPoint, vertex);
            if (miterLength / halfWidth <= MathF.Max(miterLimit, 1f))
            {
                result.LineTo(ToPdfPoint(miterPoint));
                result.LineTo(ToPdfPoint(end));
                return;
            }
        }

        result.LineTo(ToPdfPoint(end));
    }

    private static void AddCap(PdfPathBuilder result, Vector2 pivot, Vector2 outwardTangent, float halfWidth, PdfStrokeCap cap)
    {
        switch (cap)
        {
            case PdfStrokeCap.Round:
            {
                float startAngle = AngleDegreesOf(LeftNormal(outwardTangent, 1f));
                AddArc(result, pivot, halfWidth, startAngle, -180f);
                break;
            }
            case PdfStrokeCap.Square:
            {
                Vector2 start = Offset(pivot, outwardTangent, halfWidth);
                Vector2 end = Offset(pivot, -outwardTangent, halfWidth);
                Vector2 startExtended = start + (outwardTangent * halfWidth);
                Vector2 endExtended = end + (outwardTangent * halfWidth);
                result.LineTo(ToPdfPoint(startExtended));
                result.LineTo(ToPdfPoint(endExtended));
                result.LineTo(ToPdfPoint(end));
                break;
            }
            default:
            {
                Vector2 end = Offset(pivot, -outwardTangent, halfWidth);
                result.LineTo(ToPdfPoint(end));
                break;
            }
        }
    }

    /// <summary>
    /// Emits a circular arc of <paramref name="radius"/> centered at <paramref name="center"/>, starting at
    /// <paramref name="startAngleDegrees"/> and sweeping by <paramref name="sweepDegrees"/> (signed, degrees),
    /// as one or more cubic Bézier segments (the standard tan(sweep/4) arc-to-cubic approximation, exact at
    /// the endpoints and accurate to a fraction of a percent per segment). The builder's current point must
    /// already be the arc's start point.
    /// </summary>
    private static void AddArc(PdfPathBuilder result, Vector2 center, float radius, float startAngleDegrees, float sweepDegrees)
    {
        int pieceCount = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(sweepDegrees) / 90f));
        float pieceSweep = sweepDegrees / pieceCount;

        float angle = startAngleDegrees;
        for (int i = 0; i < pieceCount; i++)
        {
            float nextAngle = angle + pieceSweep;

            float startRadians = angle * MathF.PI / 180f;
            float endRadians = nextAngle * MathF.PI / 180f;
            float alpha = (4f / 3f) * MathF.Tan((endRadians - startRadians) / 4f);

            float cosStart = MathF.Cos(startRadians);
            float sinStart = MathF.Sin(startRadians);
            float cosEnd = MathF.Cos(endRadians);
            float sinEnd = MathF.Sin(endRadians);

            Vector2 control1 = new(center.X + (radius * (cosStart - (alpha * sinStart))), center.Y + (radius * (sinStart + (alpha * cosStart))));
            Vector2 control2 = new(center.X + (radius * (cosEnd + (alpha * sinEnd))), center.Y + (radius * (sinEnd - (alpha * cosEnd))));
            Vector2 end = new(center.X + (radius * cosEnd), center.Y + (radius * sinEnd));

            result.CubicTo(ToPdfPoint(control1), ToPdfPoint(control2), ToPdfPoint(end));
            angle = nextAngle;
        }
    }
}
