using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// State a set-OCG-state action applies to the groups that follow it in its /State array.
/// </summary>
[PdfEnum]
public enum PdfOptionalContentStateOperation
{
    /// <summary>
    /// Unknown operation; the groups that follow it are left unchanged.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Set the groups to ON.
    /// </summary>
    [PdfEnumValue("ON")]
    On,

    /// <summary>
    /// Set the groups to OFF.
    /// </summary>
    [PdfEnumValue("OFF")]
    Off,

    /// <summary>
    /// Reverse the state of the groups.
    /// </summary>
    [PdfEnumValue("Toggle")]
    Toggle
}
