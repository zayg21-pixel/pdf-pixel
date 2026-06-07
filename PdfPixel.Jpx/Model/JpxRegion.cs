namespace PdfPixel.Jpx.Model;

/// <summary>
/// A rectangular region expressed in full-resolution (not descaled) JPX image coordinates.
/// </summary>
public readonly struct JpxRegion
{
    /// <summary>
    /// Initializes a new <see cref="JpxRegion"/> with the given bounds.
    /// </summary>
    /// <param name="x">Left edge, in full-resolution image samples.</param>
    /// <param name="y">Top edge, in full-resolution image samples.</param>
    /// <param name="width">Width, in full-resolution image samples.</param>
    /// <param name="height">Height, in full-resolution image samples.</param>
    public JpxRegion(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Left edge, in full-resolution image samples.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Top edge, in full-resolution image samples.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Width, in full-resolution image samples.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height, in full-resolution image samples.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Right edge, in full-resolution image samples.
    /// </summary>
    public int Right => X + Width;

    /// <summary>
    /// Bottom edge, in full-resolution image samples.
    /// </summary>
    public int Bottom => Y + Height;

    /// <summary>
    /// Determines whether this region overlaps <paramref name="other"/>. Regions that only
    /// touch at an edge do not intersect.
    /// </summary>
    public bool IntersectsWith(in JpxRegion other)
    {
        return X < other.Right
            && other.X < Right
            && Y < other.Bottom
            && other.Y < Bottom;
    }
}
