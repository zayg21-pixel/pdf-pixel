namespace PdfPixel.Geometry;

/// <summary>
/// Identifies the kind of a <see cref="PdfPathSegment"/>. The numeric value doubles as the tag byte
/// written into <see cref="PdfPath"/>'s internal binary buffer ahead of each segment's points.
/// </summary>
public enum PdfPathSegmentType
{
    /// <summary>
    /// Starts a new subpath at a point.
    /// </summary>
    MoveTo = 0,

    /// <summary>
    /// Draws a straight line to a point.
    /// </summary>
    LineTo = 1,

    /// <summary>
    /// Draws a cubic Bézier curve through two control points to an end point.
    /// </summary>
    CubicTo = 2,

    /// <summary>
    /// Closes the current subpath with a straight line back to its start.
    /// </summary>
    Close = 3
}
