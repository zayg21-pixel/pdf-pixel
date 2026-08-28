namespace PdfPixel.Jpg.Model;

/// <summary>
/// An axis-aligned rectangle on the JPEG frame, measured in stored (not descaled) samples.
/// </summary>
public readonly struct JpgRectangle
{
    /// <summary>
    /// Initializes a new <see cref="JpgRectangle"/> with the given bounds.
    /// </summary>
    /// <param name="x">Left edge, in stored samples.</param>
    /// <param name="y">Top edge, in stored samples.</param>
    /// <param name="width">Width, in stored samples.</param>
    /// <param name="height">Height, in stored samples.</param>
    public JpgRectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Left edge, in stored samples.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Top edge, in stored samples.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Width, in stored samples.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height, in stored samples.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Right edge, in stored samples, exclusive.
    /// </summary>
    public int Right => X + Width;

    /// <summary>
    /// Bottom edge, in stored samples, exclusive.
    /// </summary>
    public int Bottom => Y + Height;
}
