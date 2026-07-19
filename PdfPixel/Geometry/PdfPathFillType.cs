namespace PdfPixel.Geometry;

/// <summary>
/// Determines which regions of a self-intersecting or overlapping <see cref="PdfPath"/> are
/// considered "inside" when it is filled or used as a clip.
/// </summary>
public enum PdfPathFillType
{
    /// <summary>
    /// A point is inside if the sum of signed crossings of the path boundary is non-zero.
    /// </summary>
    Winding,

    /// <summary>
    /// A point is inside if the number of crossings of the path boundary is odd.
    /// </summary>
    EvenOdd
}
