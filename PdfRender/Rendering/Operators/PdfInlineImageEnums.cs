using PdfRender.Text;

namespace PdfRender.Rendering.Operators;

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

/// <summary>
/// Enumerates the supported inline image filter abbreviations in PDF content streams.
/// </summary>
[PdfEnum]
public enum PdfInlineImageFilter
{
    [PdfEnumDefaultValue]
    Unknown,

    [PdfEnumValue("Fl")]
    Flate,
    [PdfEnumValue("LZW")]
    LZW,
    [PdfEnumValue("AHx")]
    ASCIIHex,
    [PdfEnumValue("A85")]
    ASCII85,
    [PdfEnumValue("RL")]
    RunLength,
    [PdfEnumValue("CCF")]
    CCITTFax,
    [PdfEnumValue("DCT")]
    DCT,
    [PdfEnumValue("JPX")]
    JPX,
    [PdfEnumValue("JB2")]
    JBIG2
}

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
