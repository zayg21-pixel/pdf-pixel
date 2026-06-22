using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// Specifies the policy for determining visibility from a set of optional content groups.
/// </summary>
[PdfEnum]
public enum PdfOptionalContentVisibilityPolicy
{
    /// <summary>
    /// Content is visible when all groups are on.
    /// </summary>
    [PdfEnumValue("AllOn")]
    [PdfEnumDefaultValue]
    AllOn,

    /// <summary>
    /// Content is visible when any group is on.
    /// </summary>
    [PdfEnumValue("AnyOn")]
    AnyOn,

    /// <summary>
    /// Content is visible when any group is off.
    /// </summary>
    [PdfEnumValue("AnyOff")]
    AnyOff,

    /// <summary>
    /// Content is visible when all groups are off.
    /// </summary>
    [PdfEnumValue("AllOff")]
    AllOff
}
