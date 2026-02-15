using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// PDF page label numbering style.
/// </summary>
[PdfEnum]
public enum PageLabelStyle
{
    [PdfEnumDefaultValue]
    [PdfEnumValue("D")]
    Decimal = 0,

    [PdfEnumValue("r")]
    LowerRoman = 1,

    [PdfEnumValue("R")]
    UpperRoman = 2,

    [PdfEnumValue("a")]
    LowerAlpha = 3,

    [PdfEnumValue("A")]
    UpperAlpha = 4
}
