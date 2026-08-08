namespace PdfPixel.Geometry;

/// <summary>
/// What one walk over a <see cref="PdfPath"/> tells about its geometry. Produced by
/// <see cref="PdfPath.GetPathInfo"/>, which gathers everything here in a single pass, for callers that
/// would otherwise walk the same path once per value they need.
/// </summary>
public readonly struct PdfPathInfo
{
    /// <summary>
    /// Initializes a new <see cref="PdfPathInfo"/>.
    /// </summary>
    public PdfPathInfo(in PdfRectangle bounds, bool isRectilinear, bool isRectangle)
    {
        Bounds = bounds;
        IsRectilinear = isRectilinear;
        IsRectangle = isRectangle;
    }

    /// <summary>
    /// The smallest rectangle containing every point of every segment, including control points of curve
    /// segments. <see cref="PdfRectangle.Empty"/> when the path has no segments.
    /// </summary>
    public PdfRectangle Bounds { get; }

    /// <summary>
    /// Whether every segment runs along an axis: no curves, and every line — including the one a close
    /// draws back to the start of its subpath — is exactly horizontal or exactly vertical. A path that is
    /// rectilinear here is rectilinear in device space under any matrix that preserves the axes, which is
    /// what lets the two be decided apart. True for a path with no segments.
    /// </summary>
    public bool IsRectilinear { get; }

    /// <summary>
    /// Whether the path is one closed axis-aligned rectangle: a single subpath, running along the axes,
    /// closing after four edges. This is what convexity comes to for a path that runs along the axes,
    /// since any such closed shape with more corners than four has one that turns back on itself. A
    /// rectangle covers everything inside its bounds, so rounding its edges to whole pixels can only move
    /// them; a shape that is not one holds members thinner than its bounds, which rounding can swallow.
    /// </summary>
    public bool IsRectangle { get; }
}
