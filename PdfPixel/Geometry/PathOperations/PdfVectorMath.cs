using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// Vector helpers shared by the path operations.
/// </summary>
internal static class PdfVectorMath
{
    public const float Epsilon = 1e-4f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PointsEqual(Vector2 a, Vector2 b) => Vector2.DistanceSquared(a, b) <= (Epsilon * Epsilon);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Direction(Vector2 from, Vector2 to) => NormalizeOrZero(to - from);

    /// <summary>
    /// The unit vector along <paramref name="delta"/>, or zero when it is shorter than
    /// <see cref="Epsilon"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 NormalizeOrZero(Vector2 delta)
        => (delta.LengthSquared() <= (Epsilon * Epsilon)) ? Vector2.Zero : Vector2.Normalize(delta);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 LeftNormal(Vector2 direction, float scale) => new Vector2(-direction.Y, direction.X) * scale;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Offset(Vector2 point, Vector2 direction, float halfWidth) => point + LeftNormal(direction, halfWidth);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AngleDegreesOf(Vector2 direction) => MathF.Atan2(direction.Y, direction.X) * 180f / MathF.PI;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AngleDegrees(Vector2 a, Vector2 b)
    {
        float cross = Vector2.Dot(LeftNormal(a, 1f), b);
        float dot = Vector2.Dot(a, b);
        return MathF.Atan2(cross, dot) * 180f / MathF.PI;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ToVector2(in PdfPoint point) => new(point.X, point.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPoint ToPdfPoint(Vector2 vector) => new(vector.X, vector.Y);

    /// <summary>
    /// Unit tangent at the midpoint (<c>t = 0.5</c>) of the cubic Bézier defined by these control points, taken
    /// from the final de Casteljau chord. Zero for a fully degenerate curve.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 CubicMidpointTangent(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
        => NormalizeOrZero((control2 + end - start - control1) * 0.25f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryIntersectLines(Vector2 p1, Vector2 d1, Vector2 p2, Vector2 d2, out Vector2 intersection)
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

    /// <summary>
    /// The point where the circles of <paramref name="radius"/> around <paramref name="firstCenter"/> and
    /// <paramref name="secondCenter"/> meet on the left of the line from the first center to the second, or on
    /// its right when <paramref name="leftSide"/> is false. The midpoint of the centers when the circles do not meet.
    /// </summary>
    public static Vector2 GetCircleIntersection(Vector2 firstCenter, Vector2 secondCenter, float radius, bool leftSide)
    {
        Vector2 delta = secondCenter - firstCenter;
        float height = MathF.Sqrt(MathF.Max(0f, (radius * radius) - (delta.LengthSquared() / 4f)));
        Vector2 sideOffset = LeftNormal(NormalizeOrZero(delta), leftSide ? height : -height);

        return ((firstCenter + secondCenter) * 0.5f) + sideOffset;
    }
}
