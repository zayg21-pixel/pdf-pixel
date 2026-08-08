using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Geometry;

internal static partial class PdfStrokeOutlineBuilder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PointsEqual(Vector2 a, Vector2 b) => Vector2.DistanceSquared(a, b) <= (Epsilon * Epsilon);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 Direction(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        return (delta.LengthSquared() <= (Epsilon * Epsilon)) ? Vector2.Zero : Vector2.Normalize(delta);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 LeftNormal(Vector2 direction, float scale) => new Vector2(-direction.Y, direction.X) * scale;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 Offset(Vector2 point, Vector2 direction, float halfWidth) => point + LeftNormal(direction, halfWidth);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float AngleDegreesOf(Vector2 direction) => MathF.Atan2(direction.Y, direction.X) * 180f / MathF.PI;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float AngleDegrees(Vector2 a, Vector2 b)
    {
        float cross = Vector2.Dot(LeftNormal(a, 1f), b);
        float dot = Vector2.Dot(a, b);
        return MathF.Atan2(cross, dot) * 180f / MathF.PI;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 ToVector2(in PdfPoint point) => new(point.X, point.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfPoint ToPdfPoint(Vector2 vector) => new(vector.X, vector.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 CubicMidpoint(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
        => (start + (3f * control1) + (3f * control2) + end) * 0.125f;

    /// <summary>
    /// Unit tangent at the midpoint (<c>t = 0.5</c>) of the cubic Bézier defined by these control points, taken
    /// from the final de Casteljau chord. Zero for a fully degenerate curve.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 CubicMidpointTangent(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        Vector2 q0 = Vector2.Lerp(start, control1, 0.5f);
        Vector2 q1 = Vector2.Lerp(control1, control2, 0.5f);
        Vector2 q2 = Vector2.Lerp(control2, end, 0.5f);
        Vector2 r0 = Vector2.Lerp(q0, q1, 0.5f);
        Vector2 r1 = Vector2.Lerp(q1, q2, 0.5f);
        return Direction(r0, r1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryIntersectLines(Vector2 p1, Vector2 d1, Vector2 p2, Vector2 d2, out Vector2 intersection)
    {
        float denominator = Vector2.Dot(LeftNormal(d1, 1f), d2);
        if (MathF.Abs(denominator) <= Epsilon)
        {
            intersection = default;
            return false;
        }

        Vector2 delta = p2 - p1;
        float t = Vector2.Dot(LeftNormal(delta, 1f), d2) / denominator;

        intersection = p1 + (d1 * t);
        return true;
    }
}
