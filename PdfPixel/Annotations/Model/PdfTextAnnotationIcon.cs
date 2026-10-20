using PdfPixel.Text;

namespace PdfPixel.Annotations.Model;

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
    Insert,

    /// <summary>
    /// Check icon.
    /// </summary>
    [PdfEnumValue("Check")]
    Check,

    /// <summary>
    /// Circle icon.
    /// </summary>
    [PdfEnumValue("Circle")]
    Circle,

    /// <summary>
    /// Cross icon.
    /// </summary>
    [PdfEnumValue("Cross")]
    Cross,

    /// <summary>
    /// CrossHairs icon.
    /// </summary>
    [PdfEnumValue("CrossHairs")]
    CrossHairs,

    /// <summary>
    /// RightArrow icon.
    /// </summary>
    [PdfEnumValue("RightArrow")]
    RightArrow,

    /// <summary>
    /// RightPointer icon.
    /// </summary>
    [PdfEnumValue("RightPointer")]
    RightPointer,

    /// <summary>
    /// Star icon.
    /// </summary>
    [PdfEnumValue("Star")]
    Star,

    /// <summary>
    /// UpArrow icon.
    /// </summary>
    [PdfEnumValue("UpArrow")]
    UpArrow,

    /// <summary>
    /// UpLeftArrow icon.
    /// </summary>
    [PdfEnumValue("UpLeftArrow")]
    UpLeftArrow
}
