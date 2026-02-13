using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Icon types for Text annotations as allowed by the PDF specification.
/// </summary>
[PdfEnum]
public enum PdfTextAnnotationIcon
{
    /// <summary>
    /// Note icon (default).
    /// </summary>
    [PdfEnumValue("Note")]
    [PdfEnumDefaultValue]
    Note,

    /// <summary>
    /// Comment icon.
    /// </summary>
    [PdfEnumValue("Comment")]
    Comment,

    /// <summary>
    /// Key icon.
    /// </summary>
    [PdfEnumValue("Key")]
    Key,

    /// <summary>
    /// Help icon.
    /// </summary>
    [PdfEnumValue("Help")]
    Help,

    /// <summary>
    /// NewParagraph icon.
    /// </summary>
    [PdfEnumValue("NewParagraph")]
    NewParagraph,

    /// <summary>
    /// Paragraph icon.
    /// </summary>
    [PdfEnumValue("Paragraph")]
    Paragraph,

    /// <summary>
    /// Insert icon.
    /// </summary>
    [PdfEnumValue("Insert")]
    Insert
}
