using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Enumerates the supported inline image property abbreviations in PDF content streams.
/// </summary>
[PdfEnum]
public enum PdfInlineImageProperty
{
    /// <summary>
    /// Unrecognized property key.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Image width in samples (<c>W</c>).
    /// </summary>
    [PdfEnumValue("W")]
    Width,

    /// <summary>
    /// Image height in samples (<c>H</c>).
    /// </summary>
    [PdfEnumValue("H")]
    Height,

    /// <summary>
    /// Number of bits per color component (<c>BPC</c>).
    /// </summary>
    [PdfEnumValue("BPC")]
    BitsPerComponent,

    /// <summary>
    /// Color space name or array (<c>CS</c>).
    /// </summary>
    [PdfEnumValue("CS")]
    ColorSpace,

    /// <summary>
    /// Decode array mapping sample values to color component ranges (<c>D</c>).
    /// </summary>
    [PdfEnumValue("D")]
    Decode,

    /// <summary>
    /// Parameters for the decode filter (<c>DP</c>).
    /// </summary>
    [PdfEnumValue("DP")]
    DecodeParms,

    /// <summary>
    /// Filter or array of filters applied to the image data (<c>F</c>).
    /// </summary>
    [PdfEnumValue("F")]
    Filter,

    /// <summary>
    /// Whether the image is a stencil mask (<c>IM</c>).
    /// </summary>
    [PdfEnumValue("IM")]
    ImageMask
}
