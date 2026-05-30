using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// PDF page label numbering style.
/// </summary>
[PdfEnum]
public enum PageLabelStyle
{
    /// <summary>
    /// Arabic decimal numerals: 1, 2, 3 … (<c>D</c>).
    /// </summary>
    [PdfEnumDefaultValue]
    [PdfEnumValue("D")]
    Decimal = 0,

    /// <summary>
    /// Lowercase Roman numerals: i, ii, iii … (<c>r</c>).
    /// </summary>
    [PdfEnumValue("r")]
    LowerRoman = 1,

    /// <summary>
    /// Uppercase Roman numerals: I, II, III … (<c>R</c>).
    /// </summary>
    [PdfEnumValue("R")]
    UpperRoman = 2,

    /// <summary>
    /// Lowercase alphabetic: a, b, c … (<c>a</c>).
    /// </summary>
    [PdfEnumValue("a")]
    LowerAlpha = 3,

    /// <summary>
    /// Uppercase alphabetic: A, B, C … (<c>A</c>).
    /// </summary>
    [PdfEnumValue("A")]
    UpperAlpha = 4
}
