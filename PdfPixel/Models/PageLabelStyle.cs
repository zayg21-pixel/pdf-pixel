using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// PDF page label numbering style.
/// </summary>
[PdfEnum]
public enum PageLabelStyle
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown = 0,

    /// <summary>
    /// Arabic decimal numerals: 1, 2, 3 … (<c>D</c>).
    /// </summary>
    [PdfEnumValue("D")]
    Decimal = 1,

    /// <summary>
    /// Lowercase Roman numerals: i, ii, iii … (<c>r</c>).
    /// </summary>
    [PdfEnumValue("r")]
    LowerRoman = 2,

    /// <summary>
    /// Uppercase Roman numerals: I, II, III … (<c>R</c>).
    /// </summary>
    [PdfEnumValue("R")]
    UpperRoman = 3,

    /// <summary>
    /// Lowercase alphabetic: a, b, c … (<c>a</c>).
    /// </summary>
    [PdfEnumValue("a")]
    LowerAlpha = 4,

    /// <summary>
    /// Uppercase alphabetic: A, B, C … (<c>A</c>).
    /// </summary>
    [PdfEnumValue("A")]
    UpperAlpha = 5
}
