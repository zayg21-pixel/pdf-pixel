using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Text decoration applied to an element's text (TextDecorationType).
/// </summary>
[PdfEnum]
public enum PdfStructureTextDecorationType
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// No text decoration.
    /// </summary>
    [PdfEnumValue("None")]
    None,

    /// <summary>
    /// A line below the text.
    /// </summary>
    [PdfEnumValue("Underline")]
    Underline,

    /// <summary>
    /// A line above the text.
    /// </summary>
    [PdfEnumValue("Overline")]
    Overline,

    /// <summary>
    /// A line through the middle of the text.
    /// </summary>
    [PdfEnumValue("LineThrough")]
    LineThrough
}
