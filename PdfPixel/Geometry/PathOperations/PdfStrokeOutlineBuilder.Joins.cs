using PdfPixel.Color.Paint;
using System;
using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

public static partial class PdfStrokeOutlineBuilder
{
    internal static void AddJoin(
        PdfPathBuilder result,
        Vector2 vertex,
        Vector2 incomingTangent,
        Vector2 outgoingTangent,
        float halfWidth,
        PdfStrokeJoin join,
        float miterLimit)
    {
        float sweep = PdfVectorMath.AngleDegrees(incomingTangent, outgoingTangent);
        if (MathF.Abs(sweep) <= PdfVectorMath.Epsilon)
        {
            return;
        }

        if (sweep > 0f)
        {
            // Concave side: route the inner contour back through the vertex before stepping out to the
            // outgoing offset. Anchoring the overlap at the centerline keeps the winding number consistent
            // when a leg is shorter than the stroke radius (the short-leg problem).
            result.LineTo(PdfVectorMath.ToPdfPoint(vertex));
            Vector2 concaveEnd = PdfVectorMath.Offset(vertex, outgoingTangent, halfWidth);
            result.LineTo(PdfVectorMath.ToPdfPoint(concaveEnd));
            return;
        }

        switch (join)
        {
            case PdfStrokeJoin.Round:
            {
                result.ArcTo(vertex, halfWidth, halfWidth, PdfVectorMath.AngleDegreesOf(PdfVectorMath.LeftNormal(incomingTangent, 1f)), sweep);
                break;
            }
            case PdfStrokeJoin.Bevel:
            {
                Vector2 end = PdfVectorMath.Offset(vertex, outgoingTangent, halfWidth);
                result.LineTo(PdfVectorMath.ToPdfPoint(end));
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
        Vector2 start = PdfVectorMath.Offset(vertex, incomingTangent, halfWidth);
        Vector2 end = PdfVectorMath.Offset(vertex, outgoingTangent, halfWidth);

        if (PdfVectorMath.TryIntersectLines(start, incomingTangent, end, outgoingTangent, out Vector2 miterPoint))
        {
            float miterLength = Vector2.Distance(miterPoint, vertex);
            if (miterLength / halfWidth <= MathF.Max(miterLimit, 1f))
            {
                result.LineTo(PdfVectorMath.ToPdfPoint(miterPoint));
                result.LineTo(PdfVectorMath.ToPdfPoint(end));
                return;
            }
        }

        result.LineTo(PdfVectorMath.ToPdfPoint(end));
    }

    private static void AddCap(PdfPathBuilder result, Vector2 pivot, Vector2 outwardTangent, float halfWidth, PdfStrokeCap cap)
    {
        switch (cap)
        {
            case PdfStrokeCap.Round:
            {
                float startAngle = PdfVectorMath.AngleDegreesOf(PdfVectorMath.LeftNormal(outwardTangent, 1f));
                result.ArcTo(pivot, halfWidth, halfWidth, startAngle, -180f);
                break;
            }
            case PdfStrokeCap.Square:
            {
                Vector2 start = PdfVectorMath.Offset(pivot, outwardTangent, halfWidth);
                Vector2 end = PdfVectorMath.Offset(pivot, -outwardTangent, halfWidth);
                Vector2 startExtended = start + (outwardTangent * halfWidth);
                Vector2 endExtended = end + (outwardTangent * halfWidth);
                result.LineTo(PdfVectorMath.ToPdfPoint(startExtended));
                result.LineTo(PdfVectorMath.ToPdfPoint(endExtended));
                result.LineTo(PdfVectorMath.ToPdfPoint(end));
                break;
            }
            default:
            {
                Vector2 end = PdfVectorMath.Offset(pivot, -outwardTangent, halfWidth);
                result.LineTo(PdfVectorMath.ToPdfPoint(end));
                break;
            }
        }
    }
}
