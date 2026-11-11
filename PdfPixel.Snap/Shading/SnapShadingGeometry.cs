using PdfPixel.Geometry;
using PdfPixel.Shading;
using System;
using System.Numerics;

namespace PdfPixel.Snap.Shading;

/// <summary>
/// The parameter of axial and radial shadings at a point (ISO 32000-2 8.7.4.5.3 and 8.7.4.5.4).
/// </summary>
internal static class SnapShadingGeometry
{
    private const float DegenerateTolerance = 1e-9f;

    /// <summary>
    /// Returns t at <paramref name="point"/>: its projection onto the axis, clamped where the shading extends.
    /// </summary>
    public static float? GetAxialParameter(PdfAxialGradient gradient, in PdfPoint point)
    {
        Vector2 axis = new(gradient.End.X - gradient.Start.X, gradient.End.Y - gradient.Start.Y);
        float lengthSquared = axis.LengthSquared();

        if (lengthSquared <= DegenerateTolerance)
        {
            return null;
        }

        Vector2 offset = new(point.X - gradient.Start.X, point.Y - gradient.Start.Y);
        float s = Vector2.Dot(offset, axis) / lengthSquared;

        return Restrict(s, gradient.ExtendStart, gradient.ExtendEnd);
    }

    /// <summary>
    /// Returns how many device pixels t runs across from 0 to 1.
    /// </summary>
    public static float GetAxialDeviceLength(PdfAxialGradient gradient, in PdfMatrix deviceMatrix)
    {
        PdfPoint start = deviceMatrix.MapPoint(gradient.Start);
        PdfPoint end = deviceMatrix.MapPoint(gradient.End);

        return new Vector2(end.X - start.X, end.Y - start.Y).Length();
    }

    /// <summary>
    /// Returns an upper bound of the device pixels t runs across: center travel plus radius change, times the largest
    /// stretch of <paramref name="deviceMatrix"/>.
    /// </summary>
    public static float GetRadialDeviceLength(PdfRadialGradient gradient, in PdfMatrix deviceMatrix)
    {
        float centerDistance = new Vector2(gradient.Center1.X - gradient.Center0.X, gradient.Center1.Y - gradient.Center0.Y).Length();
        float radiusChange = Math.Abs(gradient.Radius1 - gradient.Radius0);

        // The Frobenius norm of the linear part is never less than the longest a unit vector can become.
        float stretch = MathF.Sqrt((deviceMatrix.ScaleX * deviceMatrix.ScaleX)
            + (deviceMatrix.SkewX * deviceMatrix.SkewX)
            + (deviceMatrix.SkewY * deviceMatrix.SkewY)
            + (deviceMatrix.ScaleY * deviceMatrix.ScaleY));

        return (centerDistance + radiusChange) * stretch;
    }

    /// <summary>
    /// Returns t at <paramref name="point"/>: the largest s whose circle c(s) = c0 + s·(c1 − c0),
    /// r(s) = r0 + s·(r1 − r0) ≥ 0 passes through it, leaving 0 to 1 only where the shading extends.
    /// </summary>
    public static float? GetRadialParameter(PdfRadialGradient gradient, in PdfPoint point)
    {
        Vector2 centerDelta = new(gradient.Center1.X - gradient.Center0.X, gradient.Center1.Y - gradient.Center0.Y);
        Vector2 pointDelta = new(point.X - gradient.Center0.X, point.Y - gradient.Center0.Y);
        float radius0 = gradient.Radius0;
        float radiusDelta = gradient.Radius1 - radius0;

        // |p − c(s)|² = r(s)² is a·s² − 2·b·s + c = 0.
        double a = Vector2.Dot(centerDelta, centerDelta) - (radiusDelta * radiusDelta);
        double b = Vector2.Dot(pointDelta, centerDelta) + (radius0 * radiusDelta);
        double c = Vector2.Dot(pointDelta, pointDelta) - (radius0 * radius0);

        if (Math.Abs(a) <= DegenerateTolerance)
        {
            if (Math.Abs(b) <= DegenerateTolerance)
            {
                return null;
            }

            return Accept(c / (2 * b), gradient);
        }

        double discriminant = (b * b) - (a * c);

        if (discriminant < 0)
        {
            return null;
        }

        double root = Math.Sqrt(discriminant);
        double first = (b + root) / a;
        double second = (b - root) / a;

        return Accept(Math.Max(first, second), gradient) ?? Accept(Math.Min(first, second), gradient);
    }

    private static float? Accept(double s, PdfRadialGradient gradient)
    {
        if (double.IsNaN(s) || gradient.Radius0 + (s * (gradient.Radius1 - gradient.Radius0)) < 0)
        {
            return null;
        }

        return Restrict((float)s, gradient.ExtendStart, gradient.ExtendEnd);
    }

    private static float? Restrict(float s, bool extendStart, bool extendEnd)
    {
        if (s < 0)
        {
            return extendStart ? 0f : null;
        }

        if (s > 1)
        {
            return extendEnd ? 1f : null;
        }

        return s;
    }
}
