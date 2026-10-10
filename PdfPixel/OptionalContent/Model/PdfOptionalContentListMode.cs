using PdfPixel.Text;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Which groups of a configuration's /Order are displayed to the user (/ListMode).
/// </summary>
[PdfEnum]
public enum PdfOptionalContentListMode
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Display all groups in the /Order array.
    /// </summary>
    [PdfEnumValue("AllPages")]
    AllPages,

    /// <summary>
    /// Display only groups referenced by one or more visible pages.
    /// </summary>
    [PdfEnumValue("VisiblePages")]
    VisiblePages
}
