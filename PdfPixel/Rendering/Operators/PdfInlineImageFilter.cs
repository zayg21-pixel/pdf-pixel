using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

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
