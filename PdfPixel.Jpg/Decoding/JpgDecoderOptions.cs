using PdfPixel.Jpg.Color;

namespace PdfPixel.Jpg.Decoding;

/// <summary>
/// Caller supplied inputs controlling how a JPEG image is decoded. Geometry the decoders derive from
/// the header lives in <see cref="JpgDecodingParameters"/>.
/// </summary>
public sealed class JpgDecoderOptions
{
    /// <summary>
    /// Default instance with standard settings suitable for standalone JPEG files.
    /// </summary>
    public static readonly JpgDecoderOptions Default = new();

    /// <summary>
    /// When true, inverts all CMYK channel values (255 - x) after color conversion.
    /// Set to true for standalone JPEG files (non-PDF), false in the PDF pipeline.
    /// </summary>
    public bool InvertCmykColors { get; set; } = true;

    /// <summary>
    /// Controls YCbCr/YCCK color space detection for the decoder.
    /// </summary>
    public JpgYuvMode YuvMode { get; set; } = JpgYuvMode.Default;

    /// <summary>
    /// Power-of-two reduction (1, 2, 4 or 8) applied to the reconstructed image size. Above 1 the
    /// decoder reconstructs fewer samples per data unit instead of reconstructing and discarding them.
    /// </summary>
    public int DescaleFactor { get; set; } = 1;
}
