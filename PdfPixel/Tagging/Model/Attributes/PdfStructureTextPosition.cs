using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Position of an element relative to the surrounding content (TextPosition, PDF 2.0).
/// </summary>
[PdfEnum]
public enum PdfStructureTextPosition
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Elevated, like a superscript.
    /// </summary>
    [PdfEnumValue("Sup")]
    Superscript,

    /// <summary>
    /// Lowered, like a subscript.
    /// </summary>
    [PdfEnumValue("Sub")]
    Subscript,

    /// <summary>
    /// Neither elevated nor lowered.
    /// </summary>
    [PdfEnumValue("Normal")]
    Normal
}
