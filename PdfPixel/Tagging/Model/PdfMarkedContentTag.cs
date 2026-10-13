using PdfPixel.Text;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// Tag operand of a marked content sequence or point.
/// </summary>
[PdfEnum]
public enum PdfMarkedContentTag
{
    /// <summary>
    /// Tag not defined by a specification.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// Optional content.
    /// </summary>
    [PdfEnumValue("OC")]
    OptionalContent,

    /// <summary>
    /// Associated files.
    /// </summary>
    [PdfEnumValue("AF")]
    AssociatedFiles,

    /// <summary>
    /// Artifact.
    /// </summary>
    [PdfEnumValue("Artifact")]
    Artifact,

    /// <summary>
    /// Span.
    /// </summary>
    [PdfEnumValue("Span")]
    Span
}
