using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Numbering system or bullet of a list (ListNumbering).
/// </summary>
[PdfEnum]
public enum PdfStructureListNumbering
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// No numbering system.
    /// </summary>
    [PdfEnumValue("None")]
    None,

    /// <summary>
    /// Unordered list with unspecified bullets (PDF 2.0).
    /// </summary>
    [PdfEnumValue("Unordered")]
    Unordered,

    /// <summary>
    /// List of terms and definitions (PDF 2.0).
    /// </summary>
    [PdfEnumValue("Description")]
    Description,

    /// <summary>
    /// Solid circular bullet.
    /// </summary>
    [PdfEnumValue("Disc")]
    Disc,

    /// <summary>
    /// Open circular bullet.
    /// </summary>
    [PdfEnumValue("Circle")]
    Circle,

    /// <summary>
    /// Solid square bullet.
    /// </summary>
    [PdfEnumValue("Square")]
    Square,

    /// <summary>
    /// Ordered list with unspecified numbering (PDF 2.0).
    /// </summary>
    [PdfEnumValue("Ordered")]
    Ordered,

    /// <summary>
    /// Decimal Arabic numerals.
    /// </summary>
    [PdfEnumValue("Decimal")]
    Decimal,

    /// <summary>
    /// Uppercase Roman numerals.
    /// </summary>
    [PdfEnumValue("UpperRoman")]
    UpperRoman,

    /// <summary>
    /// Lowercase Roman numerals.
    /// </summary>
    [PdfEnumValue("LowerRoman")]
    LowerRoman,

    /// <summary>
    /// Uppercase letters.
    /// </summary>
    [PdfEnumValue("UpperAlpha")]
    UpperAlpha,

    /// <summary>
    /// Lowercase letters.
    /// </summary>
    [PdfEnumValue("LowerAlpha")]
    LowerAlpha
}
