using PdfPixel.Geometry;
using PdfPixel.Objects;
using System;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Annotation geometry: builds and measures the paths drawn from parsed annotation points.
/// </summary>
internal static class PdfAnnotationGeometry
{
    /// <summary>
    /// Creates an open path connecting <paramref name="points"/> with straight lines.
    /// </summary>
    /// <returns>The path, or null if there are fewer than two points.</returns>
    public static PdfPath? CreatePolylinePath(PdfPoint[] points)
    {
        if (points.Length < 2)
        {
            return null;
        }

        PdfPathBuilder builder = new();
        builder.MoveTo(points[0]);

        for (int index = 1; index < points.Length; index++)
        {
            builder.LineTo(points[index]);
        }

        return builder.ToPath();
    }

    /// <summary>
    /// Creates a closed path connecting <paramref name="points"/> with straight lines.
    /// </summary>
    /// <returns>The path, or null if there are fewer than three points.</returns>
    public static PdfPath? CreatePolygonPath(PdfPoint[] points)
    {
        if (points.Length < 3)
        {
            return null;
        }

        PdfPathBuilder builder = new();
        builder.MoveTo(points[0]);

        for (int index = 1; index < points.Length; index++)
        {
            builder.LineTo(points[index]);
        }

        builder.Close();

        return builder.ToPath();
    }

    /// <summary>
    /// Returns <paramref name="path"/> with its last subpath closed.
    /// </summary>
    public static PdfPath ClosePath(PdfPath path)
    {
        PdfPathBuilder builder = new();
        builder.AddPath(path);
        builder.Close();

        return builder.ToPath();
    }

    /// <summary>
    /// Returns the first point of <paramref name="path"/>.
    /// </summary>
    /// <returns>The first point, or null if <paramref name="path"/> is null or empty.</returns>
    public static PdfPoint? GetStartPoint(PdfPath? path)
    {
        if (path == null)
        {
            return null;
        }

        PdfPathSegmentEnumerator segments = path.Segments.GetEnumerator();

        if (!segments.MoveNext())
        {
            return null;
        }

        return segments.Current.Points[0];
    }

    /// <summary>
    /// Returns the rectangle of an annotation drawn as <paramref name="path"/> with a stroke of
    /// <paramref name="lineWidth"/>: <paramref name="declaredRectangle"/> when it overlaps the stroked path,
    /// otherwise the bounds of the stroked path.
    /// </summary>
    /// <returns>The rectangle, or <paramref name="declaredRectangle"/> if <paramref name="path"/> is null.</returns>
    public static PdfRectangle GetFallbackRectangle(in PdfRectangle declaredRectangle, PdfPath? path, float lineWidth)
    {
        if (path == null)
        {
            return declaredRectangle;
        }

        PdfRectangle strokeBounds = path.GetBounds().Inflate(2f * lineWidth);

        return (PdfRectangle.IntersectsWith(declaredRectangle, strokeBounds)) ? declaredRectangle : strokeBounds;
    }

    /// <summary>
    /// Finds the two ends of <paramref name="path"/> and, for each, the point the path leaves it toward: the
    /// next point for a line segment, the nearest distinct control point for a curve.
    /// </summary>
    /// <returns>False when the path has no segment after its first point.</returns>
    public static bool TryGetPathEnds(PdfPath path, out PdfPoint start, out PdfPoint afterStart, out PdfPoint end, out PdfPoint beforeEnd)
    {
        start = default;
        afterStart = default;
        end = default;
        beforeEnd = default;

        var hasSegment = false;
        PdfPoint currentPoint = default;

        foreach (PdfPathSegment segment in path.Segments)
        {
            switch (segment.Type)
            {
                case PdfPathSegmentType.MoveTo:
                {
                    start = segment.Points[0];
                    currentPoint = segment.Points[0];
                    break;
                }
                case PdfPathSegmentType.LineTo:
                {
                    if (!hasSegment)
                    {
                        afterStart = segment.Points[0];
                    }

                    beforeEnd = currentPoint;
                    end = segment.Points[0];
                    currentPoint = segment.Points[0];
                    hasSegment = true;
                    break;
                }
                case PdfPathSegmentType.CubicTo:
                {
                    if (!hasSegment)
                    {
                        afterStart = GetFirstDistinctPoint(currentPoint, segment.Points[0], segment.Points[1], segment.Points[2]);
                    }

                    beforeEnd = GetFirstDistinctPoint(segment.Points[2], segment.Points[1], segment.Points[0], currentPoint);
                    end = segment.Points[2];
                    currentPoint = segment.Points[2];
                    hasSegment = true;
                    break;
                }
            }
        }

        return hasSegment;
    }

    private static PdfPoint GetFirstDistinctPoint(in PdfPoint anchor, in PdfPoint first, in PdfPoint second, in PdfPoint third)
    {
        if (first != anchor)
        {
            return first;
        }

        if (second != anchor)
        {
            return second;
        }

        return third;
    }
}
