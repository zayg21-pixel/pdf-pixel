namespace PdfPixel.Fonts.Model;

/// <summary>
/// Tag byte written into a <see cref="PdfFontPathBuilder"/>'s encoded buffer ahead of each
/// segment's points.
/// </summary>
internal enum PdfFontPathSegmentType
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
