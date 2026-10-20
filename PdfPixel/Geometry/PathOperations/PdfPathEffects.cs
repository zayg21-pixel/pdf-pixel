using System;
using System.Collections.Generic;
using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// Effects that rebuild a <see cref="PdfPath"/> into a new path with an altered outline.
/// </summary>
public static class PdfPathEffects
{
    private const float CornerTurnDegrees = 30f;
    private const int AreaSamplesPerSegment = 4;

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
        PdfCurveLengthBuffer lengthBuffer = new();
        List<PdfContourMeasure> contours = PdfPathMeasure.GetContours(path);

        for (int contourIndex = 0; contourIndex < contours.Count; contourIndex++)
        {
            PdfContourMeasure contour = contours[contourIndex];

            if (!contour.IsClosed || !AddCloudContour(result, contour, radius, spacing, overlapDegrees, lengthBuffer))
            {
                contour.AppendTo(result);
            }
        }

        return result.Detach(path.FillType);
    }

    /// <summary>
    /// Writes the cloud boundary for one closed contour into <paramref name="result"/>. Returns false, writing
    /// nothing, when the contour encloses no area.
    /// </summary>
    private static bool AddCloudContour(
        PdfPathBuilder result,
        PdfContourMeasure contour,
        float radius,
        float spacing,
        float overlapDegrees,
        PdfCurveLengthBuffer lengthBuffer)
    {
        List<IPdfContourSegment> segments = GetDrawnSegments(contour.Segments);

        if (segments.Count == 0)
        {
            return false;
        }

        float signedArea = GetSignedArea(segments);

        if (signedArea == 0f)
        {
            return false;
        }

        bool isCounterClockwise = signedArea > 0f;
        List<int> corners = FindCorners(segments);
        List<Vector2> centers = [];

        if (corners.Count == 0)
        {
            AddRunCenters(segments, 0, segments.Count, spacing, lengthBuffer, centers);
        }
        else
        {
            for (int cornerIndex = 0; cornerIndex < corners.Count; cornerIndex++)
            {
                int startIndex = corners[cornerIndex];
                int endIndex = corners[(cornerIndex + 1) % corners.Count];
                int segmentCount = (endIndex - startIndex + segments.Count) % segments.Count;

                if (segmentCount == 0)
                {
                    segmentCount = segments.Count;
                }

                AddRunCenters(segments, startIndex, segmentCount, spacing, lengthBuffer, centers);
            }
        }

        if (centers.Count < 2)
        {
            return false;
        }

        var intersections = new Vector2[centers.Count];

        for (int index = 0; index < centers.Count; index++)
        {
            intersections[index] = PdfVectorMath.GetCircleIntersection(centers[index], centers[(index + 1) % centers.Count], radius, leftSide: !isCounterClockwise);
        }

        result.MoveTo(PdfVectorMath.ToPdfPoint(intersections[intersections.Length - 1]));

        for (int index = 0; index < centers.Count; index++)
        {
            Vector2 arcStart = intersections[(index - 1 + intersections.Length) % intersections.Length];
            AddCircleArc(result, centers[index], radius, arcStart, intersections[index], isCounterClockwise, overlapDegrees);
        }

        result.Close();

        return true;
    }

    private static List<IPdfContourSegment> GetDrawnSegments(List<IPdfContourSegment> segments)
    {
        List<IPdfContourSegment> drawnSegments = new(segments.Count);

        for (int index = 0; index < segments.Count; index++)
        {
            if (!segments[index].IsDegenerate)
            {
                drawnSegments.Add(segments[index]);
            }
        }

        return drawnSegments;
    }

    /// <summary>
    /// Signed area enclosed by <paramref name="segments"/>, from <see cref="AreaSamplesPerSegment"/> points on each.
    /// </summary>
    private static float GetSignedArea(List<IPdfContourSegment> segments)
    {
        float doubledArea = 0f;
        Vector2 previous = segments[segments.Count - 1].End;

        for (int index = 0; index < segments.Count; index++)
        {
            IPdfContourSegment segment = segments[index];

            for (int sample = 0; sample < AreaSamplesPerSegment; sample++)
            {
                Vector2 point = segment.Evaluate(sample / (float)AreaSamplesPerSegment);
                doubledArea += (previous.X * point.Y) - (point.X * previous.Y);
                previous = point;
            }
        }

        return doubledArea / 2f;
    }

    /// <summary>
    /// Indices of the segments whose start turns away from the previous segment's end by more than
    /// <see cref="CornerTurnDegrees"/>.
    /// </summary>
    private static List<int> FindCorners(List<IPdfContourSegment> segments)
    {
        List<int> corners = [];

        for (int index = 0; index < segments.Count; index++)
        {
            Vector2 incomingTangent = segments[(index - 1 + segments.Count) % segments.Count].EndTangent();
            Vector2 outgoingTangent = segments[index].StartTangent();

            if (MathF.Abs(PdfVectorMath.AngleDegrees(incomingTangent, outgoingTangent)) > CornerTurnDegrees)
            {
                corners.Add(index);
            }
        }

        return corners;
    }

    /// <summary>
    /// Adds circle centers at equal arc-length spacing along the <paramref name="segmentCount"/> segments starting at
    /// <paramref name="startIndex"/>, including the start point and excluding the end point.
    /// </summary>
    private static void AddRunCenters(
        List<IPdfContourSegment> segments,
        int startIndex,
        int segmentCount,
        float spacing,
        PdfCurveLengthBuffer lengthBuffer,
        List<Vector2> centers)
    {
        float runLength = 0f;

        for (int offset = 0; offset < segmentCount; offset++)
        {
            runLength += segments[(startIndex + offset) % segments.Count].CreateLengthLookup(lengthBuffer).Length;
        }

        int centerCount = Math.Max(1, (int)MathF.Ceiling(runLength / spacing));
        float step = runLength / centerCount;

        centers.Add(segments[startIndex].Start);

        int nextCenter = 1;
        float segmentStartDistance = 0f;

        for (int offset = 0; offset < segmentCount && nextCenter < centerCount; offset++)
        {
            IPdfContourSegment segment = segments[(startIndex + offset) % segments.Count];
            PdfLengthLookup lengthLookup = segment.CreateLengthLookup(lengthBuffer);
            float segmentEndDistance = segmentStartDistance + lengthLookup.Length;

            while (nextCenter < centerCount && nextCenter * step <= segmentEndDistance)
            {
                float t = lengthLookup.ParameterAt((nextCenter * step) - segmentStartDistance);
                centers.Add(segment.Evaluate(t));
                nextCenter++;
            }

            segmentStartDistance = segmentEndDistance;
        }
    }

    /// <summary>
    /// Adds the arc of the circle of <paramref name="radius"/> around <paramref name="center"/> from
    /// <paramref name="start"/> to <paramref name="end"/>, turning counterclockwise when
    /// <paramref name="isCounterClockwise"/> and clockwise otherwise. The arc runs <paramref name="overlapDegrees"/>
    /// further and back at its start on a counterclockwise outline, and at its end on a clockwise one.
    /// </summary>
    private static void AddCircleArc(
        PdfPathBuilder builder,
        Vector2 center,
        float radius,
        Vector2 start,
        Vector2 end,
        bool isCounterClockwise,
        float overlapDegrees)
    {
        float startAngleDegrees = PdfVectorMath.AngleDegreesOf(start - center);
        float endAngleDegrees = PdfVectorMath.AngleDegreesOf(end - center);
        float sweepDegrees = endAngleDegrees - startAngleDegrees;

        if (isCounterClockwise && sweepDegrees <= 0f)
        {
            sweepDegrees += 360f;
        }
        else if (!isCounterClockwise && sweepDegrees >= 0f)
        {
            sweepDegrees -= 360f;
        }

        float overlapSweep = MathF.Sign(sweepDegrees) * overlapDegrees;

        if (isCounterClockwise && overlapDegrees > 0f)
        {
            AddArcSegments(builder, center, radius, startAngleDegrees, -overlapSweep);
            AddArcSegments(builder, center, radius, startAngleDegrees - overlapSweep, overlapSweep);
        }

        AddArcSegments(builder, center, radius, startAngleDegrees, sweepDegrees);

        if (!isCounterClockwise && overlapDegrees > 0f)
        {
            float endOfArcDegrees = startAngleDegrees + sweepDegrees;
            AddArcSegments(builder, center, radius, endOfArcDegrees, overlapSweep);
            AddArcSegments(builder, center, radius, endOfArcDegrees + overlapSweep, -overlapSweep);
        }
    }

    private static void AddArcSegments(PdfPathBuilder builder, Vector2 center, float radius, float startAngleDegrees, float sweepDegrees)
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
}
