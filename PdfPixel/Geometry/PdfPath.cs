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
    public PdfPath(ReadOnlyMemory<byte> buffer, PdfPathFillType fillType)
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
