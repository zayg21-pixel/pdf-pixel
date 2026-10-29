using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// Counts how many times a <see cref="PdfPath"/> winds around a point.
/// </summary>
internal static class PdfPathWinding
{
    /// <summary>
    /// The winding number of <paramref name="path"/> around <paramref name="point"/>, curves walked along the chords
    /// they are measured by and an open subpath closed back to its start.
    /// </summary>
    public static int GetWinding(PdfPath path, Vector2 point)
    {
        int winding = 0;

        foreach (PdfContourMeasure contour in PdfPathMeasure.GetContours(path))
        {
            winding += GetContourWinding(contour, point);
        }

        return winding;
    }

    private static int GetContourWinding(PdfContourMeasure contour, Vector2 point)
    {
        int winding = 0;

        foreach (IPdfContourSegment segment in contour.Segments)
        {
            if (segment is PdfCubicContourSegment cubic)
            {
                int sampleCount = cubic.GetSampleCount();
                Vector2 previous = cubic.Start;

                for (int index = 1; index <= sampleCount; index++)
                {
                    Vector2 sample = cubic.Evaluate(index / (float)sampleCount);
                    winding += GetEdgeWinding(previous, sample, point);
                    previous = sample;
                }
            }
            else
            {
                winding += GetEdgeWinding(segment.Start, segment.End, point);
            }
        }

        if (!contour.IsClosed && contour.Segments.Count > 0)
        {
            winding += GetEdgeWinding(contour.Segments[contour.Segments.Count - 1].End, contour.Segments[0].Start, point);
        }

        return winding;
    }

    /// <summary>
    /// The winding the edge from <paramref name="start"/> to <paramref name="end"/> adds around <paramref name="point"/>:
    /// 1 when it crosses the horizontal ray to the right of the point upward, -1 when downward, otherwise 0.
    /// </summary>
    private static int GetEdgeWinding(Vector2 start, Vector2 end, Vector2 point)
    {
        float side = PdfVectorMath.Cross(end - start, point - start);

        if (start.Y <= point.Y)
        {
            return (end.Y > point.Y && side > 0) ? 1 : 0;
        }
        else
        {
            return (end.Y <= point.Y && side < 0) ? -1 : 0;
        }
    }
}
