using System;
using System.Collections.Generic;

namespace PdfPixel.Geometry;

/// <summary>
/// Effects that rebuild a <see cref="PdfPath"/> into a new path with an altered outline.
/// </summary>
public static class PdfPathEffects
{
    private const int CubicFlatteningSteps = 32;
    private const float CornerTurnRadians = MathF.PI / 6f;

    /// <summary>
    /// Returns a copy of <paramref name="path"/> whose closed subpaths are replaced by the outer boundary of circles of
    /// <paramref name="radius"/> centered along them at most <paramref name="spacing"/> apart, with a vertex on every
    /// corner. Each arc runs <paramref name="overlapDegrees"/> past one end into its neighbor and back, the same way
    /// round on every subpath. Open subpaths are copied unchanged.
    /// </summary>
    /// <param name="path">The path to rebuild.</param>
    /// <param name="radius">Circle radius.</param>
    /// <param name="spacing">Largest distance between neighboring circle centers, at most twice <paramref name="radius"/>.</param>
    /// <param name="overlapDegrees">Angle each arc runs past its end; zero for none.</param>
    public static PdfPath ApplyCloudEffect(PdfPath path, float radius, float spacing, float overlapDegrees)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (!(radius > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        if (!(spacing > 0f && spacing <= 2f * radius))
        {
            throw new ArgumentOutOfRangeException(nameof(spacing));
        }

        if (!(overlapDegrees >= 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(overlapDegrees));
        }

        PdfPathBuilder result = new();
        PdfPathBuilder subpath = new();
        List<PdfPoint> outline = [];
        List<bool> junctions = [];

        foreach (PdfPathSegment segment in path.Segments)
        {
            ReadOnlySpan<PdfPoint> points = segment.Points;

            switch (segment.Type)
            {
                case PdfPathSegmentType.MoveTo:
                {
                    result.AddPath(subpath.Detach());
                    outline.Clear();
                    junctions.Clear();
                    subpath.MoveTo(points[0]);
                    AddOutlinePoint(outline, junctions, points[0], isJunction: true);
                    break;
                }
                case PdfPathSegmentType.LineTo:
                {
                    ResumeSubpath(subpath, outline);
                    subpath.LineTo(points[0]);
                    AddOutlinePoint(outline, junctions, points[0], isJunction: true);
                    break;
                }
                case PdfPathSegmentType.CubicTo:
                {
                    ResumeSubpath(subpath, outline);
                    subpath.CubicTo(points[0], points[1], points[2]);
                    AddFlattenedCubic(outline, junctions, points[0], points[1], points[2]);
                    break;
                }
                case PdfPathSegmentType.Close:
                {
                    subpath.Close();

                    if (outline.Count == 0)
                    {
                        break;
                    }

                    PdfPoint subpathStart = outline[0];

                    if (AddCloudSubpath(result, outline, junctions, radius, spacing, overlapDegrees))
                    {
                        subpath.Reset();
                    }
                    else
                    {
                        result.AddPath(subpath.Detach());
                    }

                    outline.Clear();
                    junctions.Clear();
                    AddOutlinePoint(outline, junctions, subpathStart, isJunction: true);
                    break;
                }
            }
        }

        result.AddPath(subpath.Detach());

        return result.Detach(path.FillType);
    }

    /// <summary>
    /// Starts <paramref name="subpath"/> at the first outline point when a segment follows a close without a move.
    /// </summary>
    private static void ResumeSubpath(PdfPathBuilder subpath, List<PdfPoint> outline)
    {
        if (subpath.IsEmpty && outline.Count > 0)
        {
            subpath.MoveTo(outline[0]);
        }
    }

    private static void AddOutlinePoint(List<PdfPoint> outline, List<bool> junctions, in PdfPoint point, bool isJunction)
    {
        if (outline.Count > 0 && outline[outline.Count - 1] == point)
        {
            if (isJunction)
            {
                junctions[junctions.Count - 1] = true;
            }

            return;
        }

        outline.Add(point);
        junctions.Add(isJunction);
    }

    private static void AddFlattenedCubic(List<PdfPoint> outline, List<bool> junctions, in PdfPoint control1, in PdfPoint control2, in PdfPoint end)
    {
        PdfPoint start = outline[outline.Count - 1];

        for (int step = 1; step <= CubicFlatteningSteps; step++)
        {
            float t = (float)step / CubicFlatteningSteps;
            float inverse = 1f - t;
            float startWeight = inverse * inverse * inverse;
            float control1Weight = 3f * inverse * inverse * t;
            float control2Weight = 3f * inverse * t * t;
            float endWeight = t * t * t;

            PdfPoint point = new(
                (startWeight * start.X) + (control1Weight * control1.X) + (control2Weight * control2.X) + (endWeight * end.X),
                (startWeight * start.Y) + (control1Weight * control1.Y) + (control2Weight * control2.Y) + (endWeight * end.Y));

            AddOutlinePoint(outline, junctions, point, isJunction: step == CubicFlatteningSteps);
        }
    }

    /// <summary>
    /// Writes the cloud boundary for one closed outline into <paramref name="result"/>. Returns false, writing
    /// nothing, when the outline encloses no area.
    /// </summary>
    private static bool AddCloudSubpath(PdfPathBuilder result, List<PdfPoint> outline, List<bool> junctions, float radius, float spacing, float overlapDegrees)
    {
        int lastIndex = outline.Count - 1;

        if (lastIndex > 0 && outline[lastIndex] == outline[0])
        {
            outline.RemoveAt(lastIndex);
            junctions.RemoveAt(lastIndex);
        }

        if (outline.Count < 3)
        {
            return false;
        }

        float signedArea = GetSignedArea(outline);

        if (signedArea == 0f)
        {
            return false;
        }

        float outwardSign = (signedArea > 0f) ? 1f : -1f;
        List<int> corners = FindCorners(outline, junctions);
        List<PdfPoint> centers = [];

        if (corners.Count == 0)
        {
            AddRunSamples(outline, 0, outline.Count, spacing, centers);
        }
        else
        {
            for (int cornerIndex = 0; cornerIndex < corners.Count; cornerIndex++)
            {
                int startIndex = corners[cornerIndex];
                int endIndex = corners[(cornerIndex + 1) % corners.Count];
                int edgeCount = (endIndex - startIndex + outline.Count) % outline.Count;

                if (edgeCount == 0)
                {
                    edgeCount = outline.Count;
                }

                AddRunSamples(outline, startIndex, edgeCount, spacing, centers);
            }
        }

        if (centers.Count < 2)
        {
            return false;
        }

        var intersections = new PdfPoint[centers.Count];

        for (int index = 0; index < centers.Count; index++)
        {
            intersections[index] = GetOuterIntersection(centers[index], centers[(index + 1) % centers.Count], radius, outwardSign);
        }

        result.MoveTo(intersections[intersections.Length - 1]);

        for (int index = 0; index < centers.Count; index++)
        {
            PdfPoint arcStart = intersections[(index - 1 + intersections.Length) % intersections.Length];
            AddCircleArc(result, centers[index], radius, arcStart, intersections[index], outwardSign, overlapDegrees);
        }

        result.Close();

        return true;
    }

    private static float GetSignedArea(List<PdfPoint> outline)
    {
        float doubledArea = 0f;

        for (int index = 0; index < outline.Count; index++)
        {
            PdfPoint current = outline[index];
            PdfPoint next = outline[(index + 1) % outline.Count];
            doubledArea += (current.X * next.Y) - (next.X * current.Y);
        }

        return doubledArea / 2f;
    }

    /// <summary>
    /// Indices of the segment junctions where the outline turns by more than <see cref="CornerTurnRadians"/>.
    /// </summary>
    private static List<int> FindCorners(List<PdfPoint> outline, List<bool> junctions)
    {
        List<int> corners = [];
        int count = outline.Count;

        for (int index = 0; index < count; index++)
        {
            if (!junctions[index])
            {
                continue;
            }

            PdfPoint previous = outline[(index - 1 + count) % count];
            PdfPoint current = outline[index];
            PdfPoint next = outline[(index + 1) % count];

            float incomingX = current.X - previous.X;
            float incomingY = current.Y - previous.Y;
            float outgoingX = next.X - current.X;
            float outgoingY = next.Y - current.Y;

            float cross = (incomingX * outgoingY) - (incomingY * outgoingX);
            float dot = (incomingX * outgoingX) + (incomingY * outgoingY);

            if (MathF.Abs(MathF.Atan2(cross, dot)) > CornerTurnRadians)
            {
                corners.Add(index);
            }
        }

        return corners;
    }

    /// <summary>
    /// Adds points at equal arc-length spacing along the <paramref name="edgeCount"/> outline edges starting at
    /// <paramref name="startIndex"/>, including the start point and excluding the end point.
    /// </summary>
    private static void AddRunSamples(List<PdfPoint> outline, int startIndex, int edgeCount, float spacing, List<PdfPoint> samples)
    {
        int count = outline.Count;
        float runLength = 0f;

        for (int edge = 0; edge < edgeCount; edge++)
        {
            runLength += GetDistance(outline[(startIndex + edge) % count], outline[(startIndex + edge + 1) % count]);
        }

        int sampleCount = Math.Max(1, (int)MathF.Ceiling(runLength / spacing));
        float step = runLength / sampleCount;

        samples.Add(outline[startIndex]);

        int currentEdge = 0;
        float edgeStartDistance = 0f;

        for (int sample = 1; sample < sampleCount; sample++)
        {
            float targetDistance = sample * step;
            PdfPoint edgeStart = outline[(startIndex + currentEdge) % count];
            PdfPoint edgeEnd = outline[(startIndex + currentEdge + 1) % count];
            float edgeLength = GetDistance(edgeStart, edgeEnd);

            while (currentEdge < edgeCount - 1 && edgeStartDistance + edgeLength < targetDistance)
            {
                edgeStartDistance += edgeLength;
                currentEdge++;
                edgeStart = edgeEnd;
                edgeEnd = outline[(startIndex + currentEdge + 1) % count];
                edgeLength = GetDistance(edgeStart, edgeEnd);
            }

            float fraction = (edgeLength > 0f) ? (targetDistance - edgeStartDistance) / edgeLength : 0f;

            samples.Add(new PdfPoint(
                edgeStart.X + ((edgeEnd.X - edgeStart.X) * fraction),
                edgeStart.Y + ((edgeEnd.Y - edgeStart.Y) * fraction)));
        }
    }

    /// <summary>
    /// The intersection of the circles of <paramref name="radius"/> around <paramref name="first"/> and
    /// <paramref name="second"/> that lies on the side given by <paramref name="outwardSign"/>.
    /// </summary>
    private static PdfPoint GetOuterIntersection(in PdfPoint first, in PdfPoint second, float radius, float outwardSign)
    {
        float deltaX = second.X - first.X;
        float deltaY = second.Y - first.Y;
        float distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));

        if (distance == 0f)
        {
            return first;
        }

        float normalX = outwardSign * deltaY / distance;
        float normalY = -outwardSign * deltaX / distance;
        float height = MathF.Sqrt(Math.Max(0f, (radius * radius) - (distance * distance / 4f)));

        return new PdfPoint(
            ((first.X + second.X) / 2f) + (normalX * height),
            ((first.Y + second.Y) / 2f) + (normalY * height));
    }

    /// <summary>
    /// Adds the arc of the circle of <paramref name="radius"/> around <paramref name="center"/> from
    /// <paramref name="start"/> to <paramref name="end"/>, turning counterclockwise for a positive
    /// <paramref name="outwardSign"/> and clockwise otherwise. The arc runs <paramref name="overlapDegrees"/>
    /// further and back at its end on a clockwise outline, and at its start on a counterclockwise one.
    /// </summary>
    private static void AddCircleArc(
        PdfPathBuilder builder,
        in PdfPoint center,
        float radius,
        in PdfPoint start,
        in PdfPoint end,
        float outwardSign,
        float overlapDegrees)
    {
        float startAngleDegrees = MathF.Atan2(start.Y - center.Y, start.X - center.X) * 180f / MathF.PI;
        float endAngleDegrees = MathF.Atan2(end.Y - center.Y, end.X - center.X) * 180f / MathF.PI;
        float sweepDegrees = endAngleDegrees - startAngleDegrees;

        if (outwardSign > 0f && sweepDegrees <= 0f)
        {
            sweepDegrees += 360f;
        }
        else if (outwardSign < 0f && sweepDegrees >= 0f)
        {
            sweepDegrees -= 360f;
        }

        float overlapSweep = MathF.Sign(sweepDegrees) * overlapDegrees;

        if (outwardSign > 0f && overlapDegrees > 0f)
        {
            AddArcSegments(builder, center, radius, startAngleDegrees, -overlapSweep);
            AddArcSegments(builder, center, radius, startAngleDegrees - overlapSweep, overlapSweep);
        }

        AddArcSegments(builder, center, radius, startAngleDegrees, sweepDegrees);

        if (outwardSign < 0f && overlapDegrees > 0f)
        {
            float endOfArcDegrees = startAngleDegrees + sweepDegrees;
            AddArcSegments(builder, center, radius, endOfArcDegrees, overlapSweep);
            AddArcSegments(builder, center, radius, endOfArcDegrees + overlapSweep, -overlapSweep);
        }
    }

    private static void AddArcSegments(PdfPathBuilder builder, in PdfPoint center, float radius, float startAngleDegrees, float sweepDegrees)
    {
        var segmentCount = (int)MathF.Ceiling(MathF.Abs(sweepDegrees) / 90f);
        float segmentSweep = sweepDegrees / segmentCount;

        for (int segment = 0; segment < segmentCount; segment++)
        {
            (PdfPoint control1, PdfPoint control2, PdfPoint arcEnd) = PdfPathExtensions.ArcSegmentToCubic(
                center.X,
                center.Y,
                radius,
                radius,
                startAngleDegrees + (segmentSweep * segment),
                segmentSweep);

            builder.CubicTo(control1, control2, arcEnd);
        }
    }

    private static float GetDistance(in PdfPoint start, in PdfPoint end)
    {
        float deltaX = end.X - start.X;
        float deltaY = end.Y - start.Y;

        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}
