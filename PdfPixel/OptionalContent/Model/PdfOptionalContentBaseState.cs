using PdfPixel.Text;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// State all optional content groups are initialized to when a configuration is applied (/BaseState).
/// </summary>
[PdfEnum]
public enum PdfOptionalContentBaseState
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// The states of all groups are turned ON.
    /// </summary>
    [PdfEnumValue("ON")]
    On,

    /// <summary>
    /// The states of all groups are turned OFF.
    /// </summary>
    [PdfEnumValue("OFF")]
    Off,

    /// <summary>
    /// The states of all groups are left unchanged.
    /// </summary>
    [PdfEnumValue("Unchanged")]
    Unchanged
}
