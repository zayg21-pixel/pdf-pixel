using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Enumerates the supported inline image color space abbreviations in PDF content streams.
/// </summary>
[PdfEnum]
public enum PdfInlineImageColorSpace
{
    [PdfEnumDefaultValue]
    Unknown,

    [PdfEnumValue("G")]
    DeviceGray,

    [PdfEnumValue("RGB")]
    DeviceRGB,

    [PdfEnumValue("CMYK")]
    DeviceCMYK,

    [PdfEnumValue("I")]
    Indexed
}
