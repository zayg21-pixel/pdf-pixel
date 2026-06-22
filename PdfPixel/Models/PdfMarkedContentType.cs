using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// Enumerates the types of marked content tags relevant for rendering.
/// </summary>
[PdfEnum]
public enum PdfMarkedContentType
{
    /// <summary>
    /// Unrecognized or unsupported marked content tag.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Optional content (/OC) — controls layer visibility.
    /// </summary>
    [PdfEnumValue("OC")]
    OptionalContent,

    /// <summary>
    /// Artifact (/Artifact) — non-logical content such as headers, footers, watermarks.
    /// </summary>
    [PdfEnumValue("Artifact")]
    Artifact,

    /// <summary>
    /// Span (/Span) — inline text structure element.
    /// </summary>
    [PdfEnumValue("Span")]
    Span,

    /// <summary>
    /// Paragraph (/P) — block-level text structure element.
    /// </summary>
    [PdfEnumValue("P")]
    Paragraph,

    /// <summary>
    /// Figure (/Figure) — graphical content structure element.
    /// </summary>
    [PdfEnumValue("Figure")]
    Figure
}
