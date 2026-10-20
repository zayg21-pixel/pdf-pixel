using System;
using System.Collections.Generic;
using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// Splits a <see cref="PdfPath"/> into the contours it is measured by.
/// </summary>
public sealed class PdfPathMeasure
{
    /// <summary>
    /// Initializes a measure over the contours of <paramref name="path"/>.
    /// </summary>
    public PdfPathMeasure(PdfPath path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        Contours = GetContours(path);
    }

    internal List<PdfContourMeasure> Contours { get; }

    internal static List<PdfContourMeasure> GetContours(PdfPath path)
    {
        List<PdfContourMeasure> result = [];
        PdfContourMeasure? current = null;
        Vector2 currentPoint = default;
        Vector2 subPathStart = default;

        foreach (PdfPathSegment segment in path.Segments)
        {
            switch (segment.Type)
            {
                case PdfPathSegmentType.MoveTo:
                {
                    if (current != null)
                    {
                        result.Add(current);
                    }

                    subPathStart = PdfVectorMath.ToVector2(segment.Points[0]);
                    currentPoint = subPathStart;
                    current = new PdfContourMeasure();
                    break;
                }
                case PdfPathSegmentType.LineTo:
                {
                    current ??= new PdfContourMeasure();
                    Vector2 end = PdfVectorMath.ToVector2(segment.Points[0]);
                    current.Segments.Add(new PdfLineContourSegment(currentPoint, end));
                    currentPoint = end;
                    break;
                }
                case PdfPathSegmentType.CubicTo:
                {
                    current ??= new PdfContourMeasure();
                    Vector2 control1 = PdfVectorMath.ToVector2(segment.Points[0]);
                    Vector2 control2 = PdfVectorMath.ToVector2(segment.Points[1]);
                    Vector2 end = PdfVectorMath.ToVector2(segment.Points[2]);
                    current.Segments.Add(new PdfCubicContourSegment(currentPoint, control1, control2, end));
                    currentPoint = end;
                    break;
                }
                case PdfPathSegmentType.Close:
                {
                    if (current != null && current.Segments.Count > 0)
                    {
                        if (!PdfVectorMath.PointsEqual(currentPoint, subPathStart))
                        {
                            current.Segments.Add(new PdfLineContourSegment(currentPoint, subPathStart));
                        }

                        current.IsClosed = true;
                        currentPoint = subPathStart;
                    }

                    break;
                }
            }
        }

        if (current != null)
        {
            result.Add(current);
        }

        return result;
    }
}
