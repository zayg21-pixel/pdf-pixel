using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// Counts how many times a <see cref="PdfPath"/> winds around a point.
/// </summary>
internal static class PdfPathWinding
{
    // Largest distance, in the path's own units, a flattened curve may stray from the true curve.
    private const float FlatteningTolerance = 0.01f;

    /// <summary>
    /// The winding number of <paramref name="path"/> around <paramref name="point"/>, curves flattened within
    /// <see cref="FlatteningTolerance"/> and an open subpath closed back to its start.
    /// </summary>
    public static int GetWinding(PdfPath path, Vector2 point)
    {
        WindingSink sink = new(point);
        PdfPathFlattener.Flatten(path, PdfMatrix.Identity, FlatteningTolerance, ref sink);

        return sink.Winding;
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

    private struct WindingSink : IPdfLineSink
    {
        private readonly Vector2 _point;

        public WindingSink(Vector2 point)
        {
            _point = point;
            Winding = 0;
        }

        public int Winding { get; private set; }

        public void AddLine(in PdfPoint lineStart, in PdfPoint lineEnd)
            => Winding += GetEdgeWinding(PdfVectorMath.ToVector2(lineStart), PdfVectorMath.ToVector2(lineEnd), _point);
    }
}
