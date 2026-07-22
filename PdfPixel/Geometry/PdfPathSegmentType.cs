namespace PdfPixel.Geometry;

/// <summary>
/// Identifies the kind of a <see cref="PdfPathSegment"/>. The numeric value doubles as the tag byte
/// written into <see cref="PdfPath"/>'s internal binary buffer ahead of each segment's points.
/// </summary>
#pragma warning disable CA1008 // No zero/"None" value: every tag byte written to the buffer is meaningful.
#pragma warning disable CA1028 // byte is the wire format for the buffer's tag byte, not an implementation detail.
public enum PdfPathSegmentType : byte
{
    /// <summary>
    /// Starts a new subpath at a point.
    /// </summary>
    MoveTo = 1,

    /// <summary>
    /// Draws a straight line to a point.
    /// </summary>
    LineTo = 2,

    /// <summary>
    /// Draws a cubic Bézier curve through two control points to an end point.
    /// </summary>
    CubicTo = 3,

    /// <summary>
    /// Closes the current subpath with a straight line back to its start.
    /// </summary>
    Close = 4
}
#pragma warning restore CA1028
#pragma warning restore CA1008
