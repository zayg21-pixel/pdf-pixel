namespace PdfPixel.Jpx.Model;

/// <summary>
/// An axis-aligned rectangle on the JPEG 2000 reference grid, measured in full-resolution
/// (not descaled) samples.
/// </summary>
public readonly struct JpxRectangle
{
    /// <summary>
    /// Initializes a new <see cref="JpxRectangle"/> with the given bounds.
    /// </summary>
    /// <param name="x">Left edge, in reference-grid samples.</param>
    /// <param name="y">Top edge, in reference-grid samples.</param>
    /// <param name="width">Width, in reference-grid samples.</param>
    /// <param name="height">Height, in reference-grid samples.</param>
    public JpxRectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Left edge, in reference-grid samples.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Top edge, in reference-grid samples.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Width, in reference-grid samples.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height, in reference-grid samples.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Right edge, in reference-grid samples, exclusive.
    /// </summary>
    public int Right => X + Width;

    /// <summary>
    /// Bottom edge, in reference-grid samples, exclusive.
    /// </summary>
    public int Bottom => Y + Height;

    /// <summary>
    /// Determines whether this rectangle overlaps <paramref name="other"/>. Rectangles that only
    /// touch at an edge do not intersect.
    /// </summary>
    public bool IntersectsWith(in JpxRectangle other)
    {
        return X < other.Right
            && other.X < Right
            && Y < other.Bottom
            && other.Y < Bottom;
    }
}
