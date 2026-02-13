using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Icon types for FileAttachment annotations as allowed by the PDF specification.
/// </summary>
[PdfEnum]
public enum PdfFileAttachmentIcon
{
    /// <summary>
    /// Unknown or unspecified.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Graph icon.
    /// </summary>
    [PdfEnumValue("Graph")]
    Graph,

    /// <summary>
    /// PushPin icon.
    /// </summary>
    [PdfEnumValue("PushPin")]
    PushPin,

    /// <summary>
    /// Paperclip icon.
    /// </summary>
    [PdfEnumValue("Paperclip")]
    Paperclip,

    /// <summary>
    /// Tag icon.
    /// </summary>
    [PdfEnumValue("Tag")]
    Tag
}
