using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Type of a non-interactive form field (Role).
/// </summary>
[PdfEnum]
public enum PdfStructurePrintFieldRole
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Radio button.
    /// </summary>
    [PdfEnumValue("rb")]
    RadioButton,

    /// <summary>
    /// Check box.
    /// </summary>
    [PdfEnumValue("cb")]
    CheckBox,

    /// <summary>
    /// Push button.
    /// </summary>
    [PdfEnumValue("pb")]
    PushButton,

    /// <summary>
    /// Text-value field.
    /// </summary>
    [PdfEnumValue("tv")]
    TextValue,

    /// <summary>
    /// List box field (PDF 2.0).
    /// </summary>
    [PdfEnumValue("lb")]
    ListBox
}
