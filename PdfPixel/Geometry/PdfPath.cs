using System;

namespace PdfPixel.Geometry;

/// <summary>
/// An immutable geometric path built from move, line, cubic curve, and close segments. Segments are
/// stored in a single binary buffer: each segment is a one-byte <see cref="PdfPathSegmentType"/> tag
/// followed by its points, since the point count for every segment type is fixed and known from the tag.
/// Build one incrementally with <see cref="PdfPathBuilder"/>.
/// </summary>
public sealed class PdfPath
{
    internal const int PointSizeBytes = sizeof(float) * 2;

    private readonly ReadOnlyMemory<byte> _buffer;

    /// <summary>
    /// Initializes a new <see cref="PdfPath"/> from an already-encoded segment buffer.
    /// </summary>
    public PdfPath(in ReadOnlyMemory<byte> buffer, PdfPathFillType fillType)
    {
        _buffer = buffer;
        FillType = fillType;
    }

    /// <summary>
    /// The segments that make up this path, in drawing order.
    /// </summary>
    public PdfPathSegmentEnumerable Segments => new(_buffer.Span);

    /// <summary>
    /// Whether this path has no segments.
    /// </summary>
    public bool IsEmpty => _buffer.IsEmpty;

    /// <summary>
    /// Determines which regions are considered "inside" when this path is filled or used as a clip.
    /// </summary>
    public PdfPathFillType FillType { get; }

    /// <summary>
    /// The raw encoded segment buffer, for consumers that copy or reuse it directly (such as
    /// <see cref="PdfPathBuilder.AddPath(PdfPath)"/>).
    /// </summary>
    internal ReadOnlyMemory<byte> Buffer => _buffer;

    /// <summary>
    /// Computes the smallest rectangle containing every point of every segment, including control points
    /// of curve segments. <see cref="PdfRectangle.Empty"/> when the path has no segments. Recomputed on
    /// every call by walking all segments.
    /// </summary>
    public PdfRectangle GetBounds()
    {
        var hasPoint = false;
        float minX = 0;
        float minY = 0;
        float maxX = 0;
        float maxY = 0;

        foreach (PdfPathSegment segment in Segments)
        {
            foreach (PdfPoint point in segment.Points)
            {
                if (!hasPoint)
                {
                    minX = point.X;
                    maxX = point.X;
                    minY = point.Y;
                    maxY = point.Y;
                    hasPoint = true;
                    continue;
                }

                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        return hasPoint ? new PdfRectangle(minX, minY, maxX, maxY) : PdfRectangle.Empty;
    }

    /// <summary>
    /// Walks every segment once and reports what that walk found. Recomputed on every call, so that a
    /// page holding a great many paths pays for the geometry of the ones it draws rather than carrying
    /// it on all of them.
    /// </summary>
    public PdfPathInfo GetPathInfo()
    {
        var hasPoint = false;
        float minX = 0;
        float minY = 0;
        float maxX = 0;
        float maxY = 0;

        var isRectilinear = true;
        int subpathCount = 0;
        int edgeCount = 0;
        PdfPoint currentPoint = default;
        PdfPoint subpathStart = default;

        foreach (PdfPathSegment segment in Segments)
        {
            switch (segment.Type)
            {
                case PdfPathSegmentType.MoveTo:
                {
                    subpathCount++;
                    currentPoint = segment.Points[0];
                    subpathStart = currentPoint;
                    break;
                }
                case PdfPathSegmentType.LineTo:
                {
                    isRectilinear = isRectilinear && SegmentIsAxisAligned(currentPoint, segment.Points[0]);
                    edgeCount += (SegmentIsDegenerate(currentPoint, segment.Points[0])) ? 0 : 1;
                    currentPoint = segment.Points[0];
                    break;
                }
                case PdfPathSegmentType.CubicTo:
                {
                    isRectilinear = false;
                    edgeCount++;
                    currentPoint = segment.Points[2];
                    break;
                }
                case PdfPathSegmentType.Close:
                {
                    // forceClose: the implicit closing line back to the subpath start runs along an axis too.
                    isRectilinear = isRectilinear && SegmentIsAxisAligned(currentPoint, subpathStart);
                    edgeCount += (SegmentIsDegenerate(currentPoint, subpathStart)) ? 0 : 1;
                    currentPoint = subpathStart;
                    break;
                }
            }

            foreach (PdfPoint point in segment.Points)
            {
                if (!hasPoint)
                {
                    minX = point.X;
                    maxX = point.X;
                    minY = point.Y;
                    maxY = point.Y;
                    hasPoint = true;
                    continue;
                }

                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        // A subpath left open is closed for filling all the same, so its closing edge counts.
        if (!SegmentIsDegenerate(currentPoint, subpathStart))
        {
            edgeCount++;
        }

        PdfRectangle bounds = hasPoint ? new PdfRectangle(minX, minY, maxX, maxY) : PdfRectangle.Empty;
        bool isRectangle = isRectilinear && subpathCount == 1 && edgeCount == 4;

        return new PdfPathInfo(bounds, isRectilinear, isRectangle);
    }

    /// <summary>
    /// Returns a new <see cref="PdfPath"/> with every point of every segment transformed by
    /// <paramref name="matrix"/>. This path is unaffected. Returns this same instance, unchanged, when
    /// <paramref name="matrix"/> is <see cref="PdfMatrix.Identity"/>.
    /// </summary>
    public PdfPath Transform(in PdfMatrix matrix)
    {
        if (matrix.IsIdentity)
        {
            return this;
        }

        PdfPathBuilder builder = new();

        foreach (PdfPathSegment segment in Segments)
        {
            switch (segment.Type)
            {
                case PdfPathSegmentType.MoveTo:
                {
                    builder.MoveTo(matrix.MapPoint(segment.Points[0]));
                    break;
                }
                case PdfPathSegmentType.LineTo:
                {
                    builder.LineTo(matrix.MapPoint(segment.Points[0]));
                    break;
                }
                case PdfPathSegmentType.CubicTo:
                {
                    builder.CubicTo(matrix.MapPoint(segment.Points[0]), matrix.MapPoint(segment.Points[1]), matrix.MapPoint(segment.Points[2]));
                    break;
                }
                case PdfPathSegmentType.Close:
                {
                    builder.Close();
                    break;
                }
            }
        }

        return builder.ToPath(FillType);
    }

    // Exact rather than tolerant: a path is classified in its own space, where a tolerance carries no
    // fixed device meaning, and a segment that misses by float noise is better drawn smooth than snapped.
    private static bool SegmentIsAxisAligned(in PdfPoint start, in PdfPoint end)
        => start.X == end.X || start.Y == end.Y;

    private static bool SegmentIsDegenerate(in PdfPoint start, in PdfPoint end)
        => start.X == end.X && start.Y == end.Y;

    internal static int GetPointCount(PdfPathSegmentType type)
    {
        return type switch
        {
            PdfPathSegmentType.MoveTo => 1,
            PdfPathSegmentType.LineTo => 1,
            PdfPathSegmentType.CubicTo => 3,
            PdfPathSegmentType.Close => 0,
            _ => 0
        };
    }
}
