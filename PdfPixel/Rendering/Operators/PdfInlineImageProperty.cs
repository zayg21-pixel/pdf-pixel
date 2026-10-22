using PdfPixel.Objects;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Enumerates the supported inline image property abbreviations in PDF content streams, with the full image
/// dictionary keys they stand for as aliases.
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
    [PdfEnumAlias("Width")]
    Width,

    /// <summary>
    /// Image height in samples (<c>H</c>).
    /// </summary>
    [PdfEnumValue("H")]
    [PdfEnumAlias("Height")]
    Height,

    /// <summary>
    /// Number of bits per color component (<c>BPC</c>).
    /// </summary>
    [PdfEnumValue("BPC")]
    [PdfEnumAlias("BitsPerComponent")]
    BitsPerComponent,

    /// <summary>
    /// Color space name or array (<c>CS</c>).
    /// </summary>
    [PdfEnumValue("CS")]
    [PdfEnumAlias("ColorSpace")]
    ColorSpace,

    /// <summary>
    /// Decode array mapping sample values to color component ranges (<c>D</c>).
    /// </summary>
    [PdfEnumValue("D")]
    [PdfEnumAlias("Decode")]
    Decode,

    /// <summary>
    /// Parameters for the decode filter (<c>DP</c>).
    /// </summary>
    [PdfEnumValue("DP")]
    [PdfEnumAlias("DecodeParms")]
    DecodeParms,

    /// <summary>
    /// Filter or array of filters applied to the image data (<c>F</c>).
    /// </summary>
    [PdfEnumValue("F")]
    [PdfEnumAlias("Filter")]
    Filter,

    /// <summary>
    /// Whether the image is a stencil mask (<c>IM</c>).
    /// </summary>
    [PdfEnumValue("IM")]
    [PdfEnumAlias("ImageMask")]
    ImageMask,

    /// <summary>
    /// Whether interpolation should be applied when scaling the image (<c>I</c>).
    /// </summary>
    [PdfEnumValue("I")]
    [PdfEnumAlias("Interpolate")]
    Interpolate,

    /// <summary>
    /// Rendering intent (<c>Intent</c>), which has no abbreviation.
    /// </summary>
    [PdfEnumValue("Intent")]
    Intent
}
