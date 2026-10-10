using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// State of a non-interactive radio button or check box (Checked).
/// </summary>
[PdfEnum]
public enum PdfStructurePrintFieldState
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Checked.
    /// </summary>
    [PdfEnumValue("on")]
    On,

    /// <summary>
    /// Not checked.
    /// </summary>
    [PdfEnumValue("off")]
    Off,

    /// <summary>
    /// Neither checked nor unchecked.
    /// </summary>
    [PdfEnumValue("neutral")]
    Neutral
}
