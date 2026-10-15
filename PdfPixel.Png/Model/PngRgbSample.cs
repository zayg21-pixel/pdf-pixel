namespace PdfPixel.Png.Model;

/// <summary>
/// A red, green and blue sample triple at the bit depth of the image.
/// </summary>
public readonly struct PngRgbSample
{
    /// <summary>
    /// Initializes a sample triple.
    /// </summary>
    /// <param name="red">Red sample.</param>
    /// <param name="green">Green sample.</param>
    /// <param name="blue">Blue sample.</param>
    public PngRgbSample(ushort red, ushort green, ushort blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>
    /// Red sample.
    /// </summary>
    public ushort Red { get; }

    /// <summary>
    /// Green sample.
    /// </summary>
    public ushort Green { get; }

    /// <summary>
    /// Blue sample.
    /// </summary>
    public ushort Blue { get; }
}
