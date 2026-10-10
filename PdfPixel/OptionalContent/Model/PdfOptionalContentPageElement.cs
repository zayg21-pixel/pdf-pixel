using PdfPixel.Text;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Pagination artifact contained by an optional content group (/PageElement /Subtype).
/// </summary>
[PdfEnum]
public enum PdfOptionalContentPageElement
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Header or footer (<c>HF</c>).
    /// </summary>
    [PdfEnumValue("HF")]
    HeaderFooter,

    /// <summary>
    /// Foreground image or graphics (<c>FG</c>).
    /// </summary>
    [PdfEnumValue("FG")]
    Foreground,

    /// <summary>
    /// Background image or graphics (<c>BG</c>).
    /// </summary>
    [PdfEnumValue("BG")]
    Background,

    /// <summary>
    /// Logo (<c>L</c>).
    /// </summary>
    [PdfEnumValue("L")]
    Logo
}
