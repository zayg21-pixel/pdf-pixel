namespace PdfPixel.Path;

/// <summary>
/// Identifies the kind of a <see cref="PdfPathSegment"/>.
/// </summary>
public enum PdfPathSegmentType
{
    /// <summary>
    /// Starts a new subpath at a point.
    /// </summary>
    MoveTo,

    /// <summary>
    /// Draws a straight line to a point.
    /// </summary>
    LineTo,

    /// <summary>
    /// Draws a cubic Bézier curve through two control points to an end point.
    /// </summary>
    CubicTo,

    /// <summary>
    /// Closes the current subpath with a straight line back to its start.
    /// </summary>
    Close
}
