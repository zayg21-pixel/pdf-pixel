using System.Collections.Generic;

namespace PdfPixel.Geometry;

/// <summary>
/// A geometric path built from move, line, cubic curve, and close segments.
/// </summary>
public sealed class PdfPath
{
    private readonly List<PdfPathSegment> _segments = [];

    /// <summary>
    /// The segments that make up this path, in drawing order.
    /// </summary>
    public IReadOnlyList<PdfPathSegment> Segments => _segments;

    /// <summary>
    /// Starts a new subpath at the given point.
    /// </summary>
    public void MoveTo(float x, float y) => MoveTo(new PdfPoint(x, y));

    /// <summary>
    /// Starts a new subpath at the given point.
    /// </summary>
    public void MoveTo(in PdfPoint point) => _segments.Add(PdfPathSegment.MoveTo(point));

    /// <summary>
    /// Draws a straight line from the current point to the given point.
    /// </summary>
    public void LineTo(float x, float y) => LineTo(new PdfPoint(x, y));

    /// <summary>
    /// Draws a straight line from the current point to the given point.
    /// </summary>
    public void LineTo(in PdfPoint point) => _segments.Add(PdfPathSegment.LineTo(point));

    /// <summary>
    /// Draws a cubic Bézier curve from the current point through two control points to an end point.
    /// </summary>
    public void CubicTo(float x1, float y1, float x2, float y2, float x3, float y3)
        => CubicTo(new PdfPoint(x1, y1), new PdfPoint(x2, y2), new PdfPoint(x3, y3));

    /// <summary>
    /// Draws a cubic Bézier curve from the current point through two control points to an end point.
    /// </summary>
    public void CubicTo(in PdfPoint control1, in PdfPoint control2, in PdfPoint end)
        => _segments.Add(PdfPathSegment.CubicTo(control1, control2, end));

    /// <summary>
    /// Closes the current subpath with a straight line back to its start.
    /// </summary>
    public void Close() => _segments.Add(PdfPathSegment.Close());
}
