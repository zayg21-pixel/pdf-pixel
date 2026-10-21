using PdfPixel.Geometry;
using PdfPixel.Models;
using System;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Annotation geometry: parses stroke lists (InkList), quadrilaterals (QuadPoints) and path operator
/// arrays (Path), and measures the paths built from them.
/// </summary>
internal static class PdfAnnotationGeometry
{
    /// <summary>
    /// Parses an array of point arrays (InkList entry) into one point list per stroke.
    /// </summary>
    /// <param name="inkList">The ink list array, or null.</param>
    /// <returns>The parsed strokes, or an empty array if <paramref name="inkList"/> is null.</returns>
    public static PdfPoint[][] ParseInkList(PdfArray? inkList)
    {
        if (inkList == null)
        {
            return Array.Empty<PdfPoint[]>();
        }

        var strokes = new PdfPoint[inkList.Count][];

        for (int index = 0; index < inkList.Count; index++)
        {
            strokes[index] = PdfPoint.ParsePoints(inkList.GetArray(index));
        }

        return strokes;
    }

    /// <summary>
    /// Parses a QuadPoints array into quadrilaterals of four points each.
    /// </summary>
    /// <param name="quadPointsArray">The QuadPoints array, or null.</param>
    /// <returns>The parsed quadrilaterals, or an empty array if the array is null or its length is not a multiple of 8.</returns>
    public static PdfPoint[][] ParseQuadrilaterals(PdfArray? quadPointsArray)
    {
        if (quadPointsArray == null)
        {
            return Array.Empty<PdfPoint[]>();
        }

        float[] quadPoints = quadPointsArray.GetFloatArray();

        if (quadPoints.Length < 8 || quadPoints.Length % 8 != 0)
        {
            return Array.Empty<PdfPoint[]>();
        }

        int quadCount = quadPoints.Length / 8;
        var quads = new PdfPoint[quadCount][];

        for (int index = 0; index < quadCount; index++)
        {
            int offset = index * 8;
            quads[index] =
            [
                new PdfPoint(quadPoints[offset + 6], quadPoints[offset + 7]),
                new PdfPoint(quadPoints[offset + 4], quadPoints[offset + 5]),
                new PdfPoint(quadPoints[offset + 0], quadPoints[offset + 1]),
                new PdfPoint(quadPoints[offset + 2], quadPoints[offset + 3])
            ];
        }

        return quads;
    }

    /// <summary>
    /// Parses a Path array of path operator operands into a path: a moveto first, then a lineto
    /// for each 2-number entry and a curveto for each 6-number entry.
    /// </summary>
    /// <param name="pathArray">The Path array, or null.</param>
    /// <returns>The parsed path, or null if the array is null, empty, or contains an entry of invalid length.</returns>
    public static PdfPath? ParsePath(PdfArray? pathArray)
    {
        if (pathArray == null || pathArray.Count == 0)
        {
            return null;
        }

        PdfArray? moveToArray = pathArray.GetArray(0);

        if (moveToArray == null || moveToArray.Count != 2)
        {
            return null;
        }

        PdfPathBuilder builder = new();
        builder.MoveTo(moveToArray.GetFloatOrDefault(0), moveToArray.GetFloatOrDefault(1));

        for (int index = 1; index < pathArray.Count; index++)
        {
            PdfArray? operandArray = pathArray.GetArray(index);

            if (operandArray?.Count == 2)
            {
                builder.LineTo(operandArray.GetFloatOrDefault(0), operandArray.GetFloatOrDefault(1));
            }
            else if (operandArray?.Count == 6)
            {
                builder.CubicTo(
                    operandArray.GetFloatOrDefault(0),
                    operandArray.GetFloatOrDefault(1),
                    operandArray.GetFloatOrDefault(2),
                    operandArray.GetFloatOrDefault(3),
                    operandArray.GetFloatOrDefault(4),
                    operandArray.GetFloatOrDefault(5));
            }
            else
            {
                return null;
            }
        }

        return builder.ToPath();
    }

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
