using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// Enumerates the types of optional content dictionaries.
/// </summary>
[PdfEnum]
public enum PdfOptionalContentType
{
    /// <summary>
    /// Unrecognized or absent type.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Optional content group (/OCG).
    /// </summary>
    [PdfEnumValue("OCG")]
    Group,

    /// <summary>
    /// Optional content membership dictionary (/OCMD).
    /// </summary>
    [PdfEnumValue("OCMD")]
    Membership
}
