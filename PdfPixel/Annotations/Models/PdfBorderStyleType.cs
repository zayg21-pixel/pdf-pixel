using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents the border style for annotations.
/// </summary>
[PdfEnum]
public enum PdfBorderStyleType
{
    /// <summary>
    /// Solid border.
    /// </summary>
    [PdfEnumValue("S")]
    [PdfEnumDefaultValue]
    Solid,

    /// <summary>
    /// Dashed border.
    /// </summary>
    [PdfEnumValue("D")]
    Dashed,

    /// <summary>
    /// Beveled (three-dimensional) border.
    /// </summary>
    [PdfEnumValue("B")]
    Beveled,

    /// <summary>
    /// Inset (three-dimensional) border.
    /// </summary>
    [PdfEnumValue("I")]
    Inset,

    /// <summary>
    /// Underline border.
    /// </summary>
    [PdfEnumValue("U")]
    Underline
}
