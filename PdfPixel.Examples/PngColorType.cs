namespace PdfPixel.Examples;

/// <summary>
/// Color types a PNG image header can declare (ISO 15948, table 8).
/// </summary>
internal enum PngColorType : byte
{
    /// <summary>
    /// One gray sample per pixel.
    /// </summary>
    Gray = 0,

    /// <summary>
    /// Red, green, and blue samples per pixel.
    /// </summary>
    Truecolor = 2,

    /// <summary>
    /// A gray sample and an alpha sample per pixel.
    /// </summary>
    GrayAlpha = 4,

    /// <summary>
    /// Red, green, blue, and alpha samples per pixel.
    /// </summary>
    TruecolorAlpha = 6
}
