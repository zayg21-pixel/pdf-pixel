using System;
using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// Turns a <see cref="PdfPath"/> into straight lines in the space a matrix maps it into, every subpath closed as a
/// fill closes it (ISO 32000-2 8.5.3.3), and every curve within a tolerance of the true curve (10.7.2).
/// </summary>
public static class PdfPathFlattener
{
    private const int MaximumCurveSteps = 1024;

    /// <summary>
    /// Writes the lines of <paramref name="path"/>, mapped through <paramref name="matrix"/>, to
    /// <paramref name="sink"/>.
    /// </summary>
    /// <param name="path">The path to flatten.</param>
    /// <param name="matrix">Maps the space of <paramref name="path"/> into the space of the lines.</param>
    /// <param name="tolerance">The largest distance, in the space of the lines, a line may stray from the curve it replaces.</param>
    /// <param name="sink">Receives the lines.</param>
    public static void Flatten<TSink>(PdfPath path, in PdfMatrix matrix, float tolerance, ref TSink sink)
        where TSink : IPdfLineSink
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        PdfPoint subpathStart = default;
        PdfPoint current = default;
        var isContourOpen = false;

        foreach (PdfPathSegment segment in path.Segments)
        {
            switch (segment.Type)
            {
                case PdfPathSegmentType.MoveTo:
                {
                    CloseContour(current, subpathStart, isContourOpen, ref sink);
                    subpathStart = matrix.MapPoint(segment.Points[0]);
                    current = subpathStart;
                    isContourOpen = false;
                    break;
                }
                case PdfPathSegmentType.LineTo:
                {
                    PdfPoint end = matrix.MapPoint(segment.Points[0]);
                    AddLine(current, end, ref sink);
                    current = end;
                    isContourOpen = true;
                    break;
                }
                case PdfPathSegmentType.CubicTo:
                {
                    PdfPoint end = matrix.MapPoint(segment.Points[2]);
                    FlattenCubic(current, matrix.MapPoint(segment.Points[0]), matrix.MapPoint(segment.Points[1]), end, tolerance, ref sink);
                    current = end;
                    isContourOpen = true;
                    break;
                }
                case PdfPathSegmentType.Close:
                {
                    CloseContour(current, subpathStart, isContourOpen, ref sink);
                    current = subpathStart;
                    isContourOpen = false;
                    break;
                }
            }
        }

        CloseContour(current, subpathStart, isContourOpen, ref sink);
    }

    private static void CloseContour<TSink>(in PdfPoint current, in PdfPoint subpathStart, bool isContourOpen, ref TSink sink)
        where TSink : IPdfLineSink
    {
        if (isContourOpen)
        {
            AddLine(current, subpathStart, ref sink);
        }
    }

    private static void AddLine<TSink>(in PdfPoint start, in PdfPoint end, ref TSink sink)
        where TSink : IPdfLineSink
    {
        if (start.X != end.X || start.Y != end.Y)
        {
            sink.AddLine(start, end);
        }
    }

    /// <summary>
    /// Writes the cubic Bézier as straight lines, as many as Wang's formula needs to stay within
    /// <paramref name="tolerance"/> of the curve.
    /// </summary>
    private static void FlattenCubic<TSink>(in PdfPoint start, in PdfPoint firstControl, in PdfPoint secondControl, in PdfPoint end, float tolerance, ref TSink sink)
        where TSink : IPdfLineSink
    {
        PdfCubicContourSegment curve = new(
            PdfVectorMath.ToVector2(start),
            PdfVectorMath.ToVector2(firstControl),
            PdfVectorMath.ToVector2(secondControl),
            PdfVectorMath.ToVector2(end));
        int steps = GetCurveSteps(curve, tolerance);
        PdfPoint previous = start;

        for (int step = 1; step < steps; step++)
        {
            PdfPoint point = PdfVectorMath.ToPdfPoint(curve.Evaluate(step / (float)steps));
            AddLine(previous, point, ref sink);
            previous = point;
        }

        AddLine(previous, end, ref sink);
    }

    private static int GetCurveSteps(in PdfCubicContourSegment curve, float tolerance)
    {
        Vector2 firstDelta = curve.Start - (2 * curve.Control1) + curve.Control2;
        Vector2 secondDelta = curve.Control1 - (2 * curve.Control2) + curve.End;
        double secondDifference = Math.Max(firstDelta.Length(), secondDelta.Length());
        double estimatedSteps = Math.Ceiling(Math.Sqrt(0.75 * secondDifference / tolerance));

        if (double.IsNaN(estimatedSteps) || estimatedSteps < 1)
        {
            return 1;
        }
        else
        {
            return (int)Math.Min(estimatedSteps, MaximumCurveSteps);
        }
    }
}
