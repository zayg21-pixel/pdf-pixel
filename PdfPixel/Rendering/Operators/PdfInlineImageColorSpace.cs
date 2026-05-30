using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Enumerates the supported inline image color space abbreviations in PDF content streams.
/// </summary>
[PdfEnum]
public enum PdfInlineImageColorSpace
{
    /// <summary>
    /// Unrecognized color space name.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Device gray color space (<c>G</c>).
    /// </summary>
    [PdfEnumValue("G")]
    DeviceGray,

    /// <summary>
    /// Device RGB color space (<c>RGB</c>).
    /// </summary>
    [PdfEnumValue("RGB")]
    DeviceRGB,

    /// <summary>
    /// Device CMYK color space (<c>CMYK</c>).
    /// </summary>
    [PdfEnumValue("CMYK")]
    DeviceCMYK,

    /// <summary>
    /// Indexed (palette-based) color space (<c>I</c>).
    /// </summary>
    [PdfEnumValue("I")]
    Indexed
}
