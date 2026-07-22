using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Geometry;

internal static partial class PdfStrokeOutlineBuilder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PointsEqual(Vector2 a, Vector2 b) => Vector2.DistanceSquared(a, b) <= (Epsilon * Epsilon);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Distance(Vector2 a, Vector2 b) => Vector2.Distance(a, b);

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
}
