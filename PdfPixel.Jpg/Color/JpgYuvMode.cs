namespace PdfPixel.Jpg.Color;

/// <summary>
/// Controls YCbCr/YCCK color-space conversion behavior during JPEG decoding.
/// </summary>
public enum JpgYuvMode
{
    /// <summary>
    /// Use Adobe APP14 marker when present; otherwise apply RGB component ID heuristic.
    /// </summary>
    Default,

    /// <summary>
    /// Never apply YCbCr→RGB or YCCK→CMYK conversion.
    /// </summary>
    NoYuv,

    /// <summary>
    /// Always apply YCbCr→RGB (3-component) or YCCK→CMYK (4-component with Adobe marker).
    /// </summary>
    ForceYuv
}
