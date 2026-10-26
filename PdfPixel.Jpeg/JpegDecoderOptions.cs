using System.Collections.Generic;

namespace PdfPixel.Jpeg;

/// <summary>
/// Caller supplied inputs controlling how a JPEG image is decoded. Geometry the decoders derive from
/// the header lives in <see cref="JpegDecodingParameters"/>.
/// </summary>
public sealed class JpegDecoderOptions
{
    /// <summary>
    /// Default instance with standard settings suitable for standalone JPEG files.
    /// </summary>
    public static readonly JpegDecoderOptions Default = new();

    /// <summary>
    /// When true, inverts all CMYK channel values (255 - x) after color conversion.
    /// Set to true for standalone JPEG files (non-PDF), false in the PDF pipeline.
    /// </summary>
    public bool InvertCmykColors { get; set; } = true;

    /// <summary>
    /// Controls YCbCr/YCCK color space detection for the decoder.
    /// </summary>
    public JpegYuvMode YuvMode { get; set; } = JpegYuvMode.Default;

    /// <summary>
    /// Power-of-two reduction (1, 2, 4 or 8) applied to the reconstructed image size. Above 1 the
    /// decoder reconstructs fewer samples per data unit instead of reconstructing and discarding them.
    /// </summary>
    public int DescaleFactor { get; set; } = 1;

    /// <summary>
    /// Regions, in stored samples, that must be reconstructed. Entropy-coded data is a single serial
    /// stream and is always read in full, but data units outside these regions are neither inverse
    /// transformed nor color converted, and the rows covering them are produced blank. Null
    /// reconstructs the whole image.
    /// </summary>
    public IReadOnlyList<JpegRectangle>? RegionsOfInterest { get; set; }
}
