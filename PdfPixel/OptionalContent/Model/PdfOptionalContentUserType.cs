using PdfPixel.Text;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// How the intended user names of an optional content group are interpreted (/User /Type).
/// </summary>
[PdfEnum]
public enum PdfOptionalContentUserType
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Names of individuals (<c>Ind</c>).
    /// </summary>
    [PdfEnumValue("Ind")]
    Individual,

    /// <summary>
    /// Titles or positions (<c>Ttl</c>).
    /// </summary>
    [PdfEnumValue("Ttl")]
    Title,

    /// <summary>
    /// Organizations (<c>Org</c>).
    /// </summary>
    [PdfEnumValue("Org")]
    Organization
}
