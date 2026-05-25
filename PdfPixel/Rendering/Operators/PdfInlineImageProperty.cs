using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Enumerates the supported inline image property abbreviations in PDF content streams.
/// </summary>
[PdfEnum]
public enum PdfInlineImageProperty
{
    [PdfEnumDefaultValue]
    Unknown,

    [PdfEnumValue("W")]
    Width,

    [PdfEnumValue("H")]
    Height,

    [PdfEnumValue("BPC")]
    BitsPerComponent,

    [PdfEnumValue("CS")]
    ColorSpace,

    [PdfEnumValue("D")]
    Decode,

    [PdfEnumValue("DP")]
    DecodeParms,

    [PdfEnumValue("F")]
    Filter,

    [PdfEnumValue("IM")]
    ImageMask
}
