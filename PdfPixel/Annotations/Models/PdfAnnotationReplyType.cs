using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents the reply type for annotation threading in PDF.
/// </summary>
[PdfEnum]
public enum PdfAnnotationReplyType
{
    /// <summary>
    /// No reply type specified or unknown.
    /// </summary>
    [PdfEnumDefaultValue]
    None,

    /// <summary>
    /// Simple reply - creates a linear thread where each annotation replies to the previous one.
    /// </summary>
    [PdfEnumValue("R")]
    Reply,

    /// <summary>
    /// Group discussion - multiple annotations can reply to the same parent annotation.
    /// </summary>
    [PdfEnumValue("Group")]
    Group
}
