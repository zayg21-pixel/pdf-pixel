using PdfPixel.Text;

namespace PdfPixel.Transparency.Model;

/// <summary>
/// Group subtypes (/S of a group XObject dictionary).
/// </summary>
[PdfEnum]
internal enum PdfGroupSubtype
{
    /// <summary>
    /// Unknown group subtype (default).
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown = 0,

    /// <summary>
    /// Transparency group subtype (/Transparency).
    /// </summary>
    [PdfEnumValue("Transparency")]
    Transparency
}
