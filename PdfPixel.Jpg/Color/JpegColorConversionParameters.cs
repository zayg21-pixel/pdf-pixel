namespace PdfPixel.Jpg.Color;

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
    ForceYuv,
}

public sealed class JpegColorConversionParameters
{
    public static readonly JpegColorConversionParameters Default = new();

    /// <summary>
    /// When true, inverts all CMYK channel values (255 - x) after color conversion.
    /// Set to true for standalone JPEG files (non-PDF), false in the PDF pipeline.
    /// </summary>
    public bool InvertCmykColors { get; set; } = true;

    /// <summary>
    /// Controls YCbCr/YCCK color space detection for the decoder.
    /// </summary>
    public JpgYuvMode YuvMode { get; set; } = JpgYuvMode.Default;
}
