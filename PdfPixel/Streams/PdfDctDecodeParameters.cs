using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Streams;

/// <summary>
/// Parsed /DecodeParms entry of a DCTDecode filter.
/// </summary>
public sealed class PdfDctDecodeParameters : PdfDecodeParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDctDecodeParameters"/> class from a /DecodeParms dictionary.
    /// </summary>
    /// <param name="dictionary">The /DecodeParms dictionary.</param>
    internal PdfDctDecodeParameters(PdfDictionary dictionary) => ColorTransform = dictionary.GetInteger(PdfTokens.ColorTransformKey);

    /// <summary>
    /// Color transformation applied to the decoded samples (/ColorTransform): 0 = none, 1 = YUV to RGB
    /// (or YUVK to CMYK). Null when absent.
    /// </summary>
    public int? ColorTransform { get; }
}
