namespace PdfPixel.Jpg.Color;

/// <summary>
/// Parameters controlling JPEG color-space conversion during decoding.
/// </summary>
public sealed class JpegColorConversionParameters
{
    /// <summary>
    /// Default instance with standard settings suitable for standalone JPEG files.
    /// </summary>
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
