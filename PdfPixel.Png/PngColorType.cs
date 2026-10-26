namespace PdfPixel.Png;

/// <summary>
/// Samples each pixel of a PNG image carries (IHDR color type).
/// </summary>
public enum PngColorType
{
    /// <summary>
    /// One gray sample.
    /// </summary>
    Grayscale = 0,

    /// <summary>
    /// Red, green and blue samples.
    /// </summary>
    Truecolor = 2,

    /// <summary>
    /// One index into <see cref="PngImageInfo.Palette"/>.
    /// </summary>
    IndexedColor = 3,

    /// <summary>
    /// A gray sample and an alpha sample.
    /// </summary>
    GrayscaleAlpha = 4,

    /// <summary>
    /// Red, green, blue and alpha samples.
    /// </summary>
    TruecolorAlpha = 6
}
