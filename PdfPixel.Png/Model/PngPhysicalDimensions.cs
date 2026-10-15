namespace PdfPixel.Png.Model;

/// <summary>
/// Intended pixel size or aspect ratio of an image (pHYs).
/// </summary>
public readonly struct PngPhysicalDimensions
{
    /// <summary>
    /// Initializes the pixel dimensions.
    /// </summary>
    /// <param name="pixelsPerUnitX">Pixels per unit along the x axis.</param>
    /// <param name="pixelsPerUnitY">Pixels per unit along the y axis.</param>
    /// <param name="unit">Unit both values are given in.</param>
    public PngPhysicalDimensions(int pixelsPerUnitX, int pixelsPerUnitY, PngUnit unit)
    {
        PixelsPerUnitX = pixelsPerUnitX;
        PixelsPerUnitY = pixelsPerUnitY;
        Unit = unit;
    }

    /// <summary>
    /// Pixels per unit along the x axis.
    /// </summary>
    public int PixelsPerUnitX { get; }

    /// <summary>
    /// Pixels per unit along the y axis.
    /// </summary>
    public int PixelsPerUnitY { get; }

    /// <summary>
    /// Unit both values are given in.
    /// </summary>
    public PngUnit Unit { get; }
}
