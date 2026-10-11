using PdfPixel.Text;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// Subtype of artifact (Subtype).
/// </summary>
[PdfEnum]
public enum PdfArtifactSubtype
{
    /// <summary>
    /// Subtype not defined by a specification.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// Running head.
    /// </summary>
    [PdfEnumValue("Header")]
    Header,

    /// <summary>
    /// Running foot.
    /// </summary>
    [PdfEnumValue("Footer")]
    Footer,

    /// <summary>
    /// Watermark.
    /// </summary>
    [PdfEnumValue("Watermark")]
    Watermark,

    /// <summary>
    /// Page number (PDF 2.0).
    /// </summary>
    [PdfEnumValue("PageNum")]
    PageNumber,

    /// <summary>
    /// Bates number (PDF 2.0).
    /// </summary>
    [PdfEnumValue("Bates")]
    Bates,

    /// <summary>
    /// Line number (PDF 2.0).
    /// </summary>
    [PdfEnumValue("LineNum")]
    LineNumber,

    /// <summary>
    /// Redaction (PDF 2.0).
    /// </summary>
    [PdfEnumValue("Redaction")]
    Redaction
}
