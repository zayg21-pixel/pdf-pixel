namespace PdfPixel.Models;

/// <summary>
/// How a clip region combines with the current clip region on the canvas.
/// </summary>
public enum PdfClipOperation
{
    /// <summary>
    /// Intersects the current clip region with the new region.
    /// </summary>
    Intersect,

    /// <summary>
    /// Subtracts the new region from the current clip region.
    /// </summary>
    Difference
}
