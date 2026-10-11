using PdfPixel.Text;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// Type of artifact (Type).
/// </summary>
[PdfEnum]
public enum PdfArtifactType
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Ancillary page features such as running heads or folios.
    /// </summary>
    [PdfEnumValue("Pagination")]
    Pagination,

    /// <summary>
    /// Cosmetic typographical or design elements.
    /// </summary>
    [PdfEnumValue("Layout")]
    Layout,

    /// <summary>
    /// Production aids such as cut marks.
    /// </summary>
    [PdfEnumValue("Page")]
    Page,

    /// <summary>
    /// Template content repeated across pages, such as full-page images or colored blocks.
    /// </summary>
    [PdfEnumValue("Background")]
    Background,

    /// <summary>
    /// Artifact content with context in the logical structure (PDF 2.0).
    /// </summary>
    [PdfEnumValue("Inline")]
    Inline
}
