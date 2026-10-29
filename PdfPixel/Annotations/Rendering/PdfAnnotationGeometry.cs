using PdfPixel.Geometry;
using PdfPixel.Geometry.PathOperations;
using PdfPixel.Objects;
using System;
using System.Numerics;

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
    /// Creates a closed path around <paramref name="rectangle"/>, with the rounded corners of
    /// <paramref name="borderStyle"/> when both of its corner radii are positive.
    /// </summary>
    public static PdfPath CreateRectanglePath(in PdfRectangle rectangle, PdfAnnotationBorderStyle? borderStyle)
    {
        PdfPathBuilder builder = new();

        if (borderStyle != null && borderStyle.HorizontalCornerRadius > 0f && borderStyle.VerticalCornerRadius > 0f)
        {
            builder.AddRoundRect(rectangle, borderStyle.HorizontalCornerRadius, borderStyle.VerticalCornerRadius);
        }
        else
        {
            builder.AddRect(rectangle);
        }

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
    /// Returns the endpoints of the line from <paramref name="start"/> to <paramref name="end"/>, moved out by leader
    /// lines of <paramref name="leaderLength"/> that start <paramref name="leaderOffset"/> away from the endpoints.
    /// </summary>
    /// <param name="start">The start point of the line.</param>
    /// <param name="end">The end point of the line.</param>
    /// <param name="leaderLength">The leader line length; positive to the left of the line, negative to its right, 0 for no leader lines.</param>
    /// <param name="leaderOffset">The non-negative distance between the endpoints and the leader lines.</param>
    /// <returns>The moved start and end points.</returns>
    public static PdfPoint[] MoveLine(in PdfPoint start, in PdfPoint end, float leaderLength, float leaderOffset)
    {
        PdfMatrix lineMatrix = CreateLineMatrix(start, end);
        float halfLength = GetDistance(start, end) / 2f;
        float distance = GetLeaderLineStart(leaderLength, leaderOffset) + leaderLength;

        return new PdfPoint[] { lineMatrix.MapPoint(new PdfPoint(-halfLength, distance)), lineMatrix.MapPoint(new PdfPoint(halfLength, distance)) };
    }

    /// <summary>
    /// Creates the leader lines of the line from <paramref name="start"/> to <paramref name="end"/>: perpendicular
    /// to it at both endpoints, from <paramref name="leaderOffset"/> away to <paramref name="leaderExtension"/> past
    /// the moved line.
    /// </summary>
    /// <param name="start">The start point of the line.</param>
    /// <param name="end">The end point of the line.</param>
    /// <param name="leaderLength">The leader line length; positive to the left of the line, negative to its right.</param>
    /// <param name="leaderExtension">The non-negative length the leader lines extend past the moved line.</param>
    /// <param name="leaderOffset">The non-negative distance between the endpoints and the leader lines.</param>
    /// <returns>The leader lines, or null when <paramref name="leaderLength"/> is 0.</returns>
    public static PdfPath? CreateLeaderLinesPath(in PdfPoint start, in PdfPoint end, float leaderLength, float leaderExtension, float leaderOffset)
    {
        if (leaderLength == 0f)
        {
            return null;
        }

        PdfMatrix lineMatrix = CreateLineMatrix(start, end);
        float halfLength = GetDistance(start, end) / 2f;
        float leaderStart = GetLeaderLineStart(leaderLength, leaderOffset);
        float leaderEnd = leaderStart + leaderLength + (MathF.Sign(leaderLength) * leaderExtension);

        PdfPathBuilder builder = new();
        builder.MoveTo(lineMatrix.MapPoint(new PdfPoint(-halfLength, leaderStart)));
        builder.LineTo(lineMatrix.MapPoint(new PdfPoint(-halfLength, leaderEnd)));
        builder.MoveTo(lineMatrix.MapPoint(new PdfPoint(halfLength, leaderStart)));
        builder.LineTo(lineMatrix.MapPoint(new PdfPoint(halfLength, leaderEnd)));

        return builder.ToPath();
    }

    /// <summary>
    /// Creates the line from <paramref name="start"/> to <paramref name="end"/>, broken where <paramref name="clearArea"/>
    /// crosses it.
    /// </summary>
    /// <param name="start">The start point of the line.</param>
    /// <param name="end">The end point of the line.</param>
    /// <param name="clearArea">
    /// The area the line is kept clear of, in the coordinates of <see cref="CreateLineMatrix"/>, or null for an unbroken line.
    /// </param>
    public static PdfPath CreateLinePath(in PdfPoint start, in PdfPoint end, PdfRectangle? clearArea)
    {
        PdfPathBuilder builder = new();

        if (clearArea != null && clearArea.Value.Top <= 0f && clearArea.Value.Bottom >= 0f)
        {
            PdfMatrix lineMatrix = CreateLineMatrix(start, end);
            float halfLength = GetDistance(start, end) / 2f;

            AddLineSegment(builder, lineMatrix, -halfLength, Math.Min(clearArea.Value.Left, halfLength));
            AddLineSegment(builder, lineMatrix, Math.Max(clearArea.Value.Right, -halfLength), halfLength);
        }
        else
        {
            builder.MoveTo(start);
            builder.LineTo(end);
        }

        return builder.ToPath();
    }

    /// <summary>
    /// Returns the distance between <paramref name="start"/> and <paramref name="end"/>.
    /// </summary>
    public static float GetDistance(in PdfPoint start, in PdfPoint end)
    {
        float deltaX = end.X - start.X;
        float deltaY = end.Y - start.Y;

        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>
    /// Creates the matrix from the coordinates of the line from <paramref name="start"/> to <paramref name="end"/> to
    /// the page: the x axis runs along the line from its midpoint, the y axis to its left.
    /// </summary>
    public static PdfMatrix CreateLineMatrix(in PdfPoint start, in PdfPoint end)
    {
        float angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);
        PdfMatrix midpoint = PdfMatrix.CreateTranslation((start.X + end.X) / 2f, (start.Y + end.Y) / 2f);

        return PdfMatrix.Concat(midpoint, PdfMatrix.CreateRotationDegrees(angle * 180 / MathF.PI));
    }

    /// <summary>
    /// Finds the two ends of <paramref name="path"/> and, for each, the point the path leaves it toward: the
    /// next point for a line segment, the nearest distinct control point for a curve.
    /// </summary>
    /// <returns>The ends, or null if <paramref name="path"/> is null or has no segment after its first point.</returns>
    public static PdfPathEnds? GetPathEnds(PdfPath? path)
    {
        if (path == null)
        {
            return null;
        }

        PdfPoint start = default;
        PdfPoint afterStart = default;
        PdfPoint end = default;
        PdfPoint beforeEnd = default;
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

        if (!hasSegment)
        {
            return null;
        }

        return new PdfPathEnds(start, afterStart, end, beforeEnd);
    }

    /// <summary>
    /// Adds the left and top edges of <paramref name="quadrilateral"/>, given bottom right, bottom left, top left, top
    /// right, each moved <paramref name="edgeInset"/> inward, running from <paramref name="endInset"/> inside the
    /// bottom edge to <paramref name="endInset"/> inside the right edge.
    /// </summary>
    public static void AddInsetLeftTopEdges(PdfPathBuilder builder, PdfPoint[] quadrilateral, float edgeInset, float endInset)
    {
        Vector2 cornerSum = PdfVectorMath.ToVector2(quadrilateral[0])
            + PdfVectorMath.ToVector2(quadrilateral[1])
            + PdfVectorMath.ToVector2(quadrilateral[2])
            + PdfVectorMath.ToVector2(quadrilateral[3]);
        Vector2 center = cornerSum / 4f;

        (Vector2 Point, Vector2 Direction) bottom = InsetEdge(quadrilateral[0], quadrilateral[1], center, endInset);
        (Vector2 Point, Vector2 Direction) left = InsetEdge(quadrilateral[1], quadrilateral[2], center, edgeInset);
        (Vector2 Point, Vector2 Direction) top = InsetEdge(quadrilateral[2], quadrilateral[3], center, edgeInset);
        (Vector2 Point, Vector2 Direction) right = InsetEdge(quadrilateral[3], quadrilateral[0], center, endInset);

        builder.MoveTo(PdfVectorMath.ToPdfPoint(PdfVectorMath.IntersectLines(left.Point, left.Direction, bottom.Point, bottom.Direction)));
        builder.LineTo(PdfVectorMath.ToPdfPoint(PdfVectorMath.IntersectLines(left.Point, left.Direction, top.Point, top.Direction)));
        builder.LineTo(PdfVectorMath.ToPdfPoint(PdfVectorMath.IntersectLines(top.Point, top.Direction, right.Point, right.Direction)));
    }

    /// <summary>
    /// The edge from <paramref name="start"/> to <paramref name="end"/> moved <paramref name="distance"/> toward
    /// <paramref name="center"/>, as a point on it and its direction.
    /// </summary>
    private static (Vector2 Point, Vector2 Direction) InsetEdge(in PdfPoint start, in PdfPoint end, Vector2 center, float distance)
    {
        Vector2 from = PdfVectorMath.ToVector2(start);
        Vector2 direction = PdfVectorMath.Direction(from, PdfVectorMath.ToVector2(end));
        Vector2 normal = PdfVectorMath.LeftNormal(direction, distance);

        if (Vector2.Dot(normal, center - from) < 0)
        {
            normal = -normal;
        }

        return (from + normal, direction);
    }

    /// <summary>
    /// Returns the distance from the line at which leader lines start, on the side of <paramref name="leaderLength"/>.
    /// </summary>
    private static float GetLeaderLineStart(float leaderLength, float leaderOffset) => MathF.Sign(leaderLength) * leaderOffset;

    private static void AddLineSegment(PdfPathBuilder builder, in PdfMatrix lineMatrix, float from, float to)
    {
        if (from >= to)
        {
            return;
        }

        builder.MoveTo(lineMatrix.MapPoint(new PdfPoint(from, 0f)));
        builder.LineTo(lineMatrix.MapPoint(new PdfPoint(to, 0f)));
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
