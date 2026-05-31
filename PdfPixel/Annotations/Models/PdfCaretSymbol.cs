using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Symbol associated with a caret annotation.
/// </summary>
[PdfEnum]
public enum PdfCaretSymbol
{
    /// <summary>
    /// Plain caret insertion point.
    /// </summary>
    [PdfEnumDefaultValue]
    [PdfEnumValue("None")]
    Caret,

    /// <summary>
    /// Paragraph break insertion point.
    /// </summary>
    [PdfEnumValue("P")]
    Paragraph
}
