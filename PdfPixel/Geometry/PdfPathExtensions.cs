using PdfPixel.Color.Paint;
using System;

namespace PdfPixel.Geometry;

/// <summary>
/// Convenience shape-building extensions for <see cref="PdfPathBuilder"/>. These shapes have no direct PDF
/// content-stream operator equivalent, so curved shapes are expanded into cubic Bézier
/// segments using the standard cubic Bézier circle/arc approximation.
/// </summary>
public static class PdfPathExtensions
{
    private const float OvalKappa = 0.5522847498f;

    /// <summary>
    /// Computes a loose bounding rectangle for <paramref name="path"/> stroked with <paramref name="strokePaint"/>:
    /// <see cref="PdfPath.GetBounds"/> inflated by half the line width (further inflated for miter joins,
    /// since their tips can extend past a plain half-width inflation). Used for pattern tiling, where an
    /// over-estimate is harmless but an under-estimate would drop visible tiles.
    /// </summary>
    public static PdfRectangle GetStrokeBounds(this PdfPath path, PdfPaint strokePaint)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        return path.GetBounds().InflateForStroke(strokePaint);
    }

    /// <summary>
    /// Grows <paramref name="bounds"/> by the reach a stroke with <paramref name="strokePaint"/> adds beyond
    /// the geometry it follows, so the result covers the stroked shape rather than its centre line.
    /// </summary>
    public static PdfRectangle InflateForStroke(this in PdfRectangle bounds, PdfPaint strokePaint)
    {
        if (strokePaint == null)
        {
            throw new ArgumentNullException(nameof(strokePaint));
        }

        PdfStrokeStyle style = strokePaint.RequireStrokeStyle();
        float halfWidth = style.LineWidth / 2f;
        float inflate = (style.LineJoin == PdfStrokeJoin.Miter) ? halfWidth * Math.Max(style.MiterLimit, 1f) : halfWidth;

        return bounds.Inflate(inflate);
    }

    /// <summary>
    /// Adds a closed rectangular contour.
    /// </summary>
    public static void AddRect(this PdfPathBuilder path, in PdfRectangle rect)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        path.MoveTo(rect.Left, rect.Top);
        path.LineTo(rect.Right, rect.Top);
        path.LineTo(rect.Right, rect.Bottom);
        path.LineTo(rect.Left, rect.Bottom);
        path.Close();
    }

    /// <summary>
    /// Adds a closed elliptical contour inscribed within <paramref name="rect"/>.
    /// </summary>
    public static void AddOval(this PdfPathBuilder path, in PdfRectangle rect)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        float centerX = rect.MidX;
        float centerY = rect.MidY;
        float radiusX = rect.Width / 2f;
        float radiusY = rect.Height / 2f;
        float offsetX = radiusX * OvalKappa;
        float offsetY = radiusY * OvalKappa;

        PdfPoint right = new(centerX + radiusX, centerY);
        PdfPoint bottom = new(centerX, centerY + radiusY);
        PdfPoint left = new(centerX - radiusX, centerY);
        PdfPoint top = new(centerX, centerY - radiusY);

        path.MoveTo(right);
        path.CubicTo(new PdfPoint(centerX + radiusX, centerY + offsetY), new PdfPoint(centerX + offsetX, centerY + radiusY), bottom);
        path.CubicTo(new PdfPoint(centerX - offsetX, centerY + radiusY), new PdfPoint(centerX - radiusX, centerY + offsetY), left);
        path.CubicTo(new PdfPoint(centerX - radiusX, centerY - offsetY), new PdfPoint(centerX - offsetX, centerY - radiusY), top);
        path.CubicTo(new PdfPoint(centerX + offsetX, centerY - radiusY), new PdfPoint(centerX + radiusX, centerY - offsetY), right);
        path.Close();
    }

    /// <summary>
    /// Adds a closed circular contour centered at <paramref name="center"/>.
    /// </summary>
    public static void AddCircle(this PdfPathBuilder path, in PdfPoint center, float radius)
        => path.AddOval(new PdfRectangle(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius));

    /// <summary>
    /// Adds a closed rectangular contour with rounded corners of radius <paramref name="cornerRadiusX"/>
    /// by <paramref name="cornerRadiusY"/>.
    /// </summary>
    public static void AddRoundRect(this PdfPathBuilder path, in PdfRectangle rect, float cornerRadiusX, float cornerRadiusY)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        float radiusX = Math.Min(cornerRadiusX, rect.Width / 2f);
        float radiusY = Math.Min(cornerRadiusY, rect.Height / 2f);
        float offsetX = radiusX * OvalKappa;
        float offsetY = radiusY * OvalKappa;

        float left = rect.Left;
        float top = rect.Top;
        float right = rect.Right;
        float bottom = rect.Bottom;

        path.MoveTo(left + radiusX, top);
        path.LineTo(right - radiusX, top);
        path.CubicTo(new PdfPoint(right - radiusX + offsetX, top), new PdfPoint(right, top + radiusY - offsetY), new PdfPoint(right, top + radiusY));
        path.LineTo(right, bottom - radiusY);
        path.CubicTo(new PdfPoint(right, bottom - radiusY + offsetY), new PdfPoint(right - radiusX + offsetX, bottom), new PdfPoint(right - radiusX, bottom));
        path.LineTo(left + radiusX, bottom);
        path.CubicTo(new PdfPoint(left + radiusX - offsetX, bottom), new PdfPoint(left, bottom - radiusY + offsetY), new PdfPoint(left, bottom - radiusY));
        path.LineTo(left, top + radiusY);
        path.CubicTo(new PdfPoint(left, top + radiusY - offsetY), new PdfPoint(left + radiusX - offsetX, top), new PdfPoint(left + radiusX, top));
        path.Close();
    }

    /// <summary>
    /// Adds an elliptical arc as a new (unclosed) subpath, inscribed within <paramref name="oval"/>.
    /// Angles follow Skia's <c>SKPath.AddArc</c> convention: degrees measured from the positive
    /// x-axis, with positive values sweeping clockwise.
    /// </summary>
    public static void AddArc(this PdfPathBuilder path, in PdfRectangle oval, float startAngleDegrees, float sweepAngleDegrees)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (sweepAngleDegrees == 0)
        {
            return;
        }

        float centerX = oval.MidX;
        float centerY = oval.MidY;
        float radiusX = oval.Width / 2f;
        float radiusY = oval.Height / 2f;

        var segmentCount = (int)MathF.Ceiling(MathF.Abs(sweepAngleDegrees) / 90f);
        float segmentSweep = sweepAngleDegrees / segmentCount;

        path.MoveTo(PointOnOval(centerX, centerY, radiusX, radiusY, startAngleDegrees));

        for (int i = 0; i < segmentCount; i++)
        {
            float segmentStart = startAngleDegrees + (segmentSweep * i);
            (PdfPoint control1, PdfPoint control2, PdfPoint end) = ArcSegmentToCubic(centerX, centerY, radiusX, radiusY, segmentStart, segmentSweep);
            path.CubicTo(control1, control2, end);
        }
    }

    private static PdfPoint PointOnOval(float centerX, float centerY, float radiusX, float radiusY, float angleDegrees)
    {
        float radians = angleDegrees * MathF.PI / 180f;
        return new PdfPoint(centerX + (radiusX * MathF.Cos(radians)), centerY + (radiusY * MathF.Sin(radians)));
    }

    private static (PdfPoint Control1, PdfPoint Control2, PdfPoint End) ArcSegmentToCubic(
        float centerX,
        float centerY,
        float radiusX,
        float radiusY,
        float startAngleDegrees,
        float sweepAngleDegrees)
    {
        float startRadians = startAngleDegrees * MathF.PI / 180f;
        float endRadians = (startAngleDegrees + sweepAngleDegrees) * MathF.PI / 180f;
        float alpha = (4f / 3f) * MathF.Tan((endRadians - startRadians) / 4f);

        float cosStart = MathF.Cos(startRadians);
        float sinStart = MathF.Sin(startRadians);
        float cosEnd = MathF.Cos(endRadians);
        float sinEnd = MathF.Sin(endRadians);

        PdfPoint control1 = new(
            centerX + (radiusX * (cosStart - (alpha * sinStart))),
            centerY + (radiusY * (sinStart + (alpha * cosStart))));

        PdfPoint control2 = new(
            centerX + (radiusX * (cosEnd + (alpha * sinEnd))),
            centerY + (radiusY * (sinEnd - (alpha * cosEnd))));

        PdfPoint end = new(
            centerX + (radiusX * cosEnd),
            centerY + (radiusY * sinEnd));

        return (control1, control2, end);
    }
}
