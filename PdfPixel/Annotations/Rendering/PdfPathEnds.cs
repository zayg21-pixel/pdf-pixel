using PdfPixel.Geometry;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// The two ends of a path and, for each, the point the path leaves it toward.
/// </summary>
internal readonly struct PdfPathEnds
{
    /// <summary>
    /// Initializes the ends of a path.
    /// </summary>
    public PdfPathEnds(in PdfPoint start, in PdfPoint afterStart, in PdfPoint end, in PdfPoint beforeEnd)
    {
        Start = start;
        AfterStart = afterStart;
        End = end;
        BeforeEnd = beforeEnd;
    }

    /// <summary>
    /// The first point of the path.
    /// </summary>
    public PdfPoint Start { get; }

    /// <summary>
    /// The point the path leaves <see cref="Start"/> toward.
    /// </summary>
    public PdfPoint AfterStart { get; }

    /// <summary>
    /// The last point of the path.
    /// </summary>
    public PdfPoint End { get; }

    /// <summary>
    /// The point the path arrives at <see cref="End"/> from.
    /// </summary>
    public PdfPoint BeforeEnd { get; }
}
