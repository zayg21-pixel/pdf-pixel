using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Block-progression alignment of content within a table cell (BlockAlign).
/// </summary>
[PdfEnum]
public enum PdfStructureBlockAlign
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Aligned with the before edge.
    /// </summary>
    [PdfEnumValue("Before")]
    Before,

    /// <summary>
    /// Centered between the before and after edges.
    /// </summary>
    [PdfEnumValue("Middle")]
    Middle,

    /// <summary>
    /// Aligned with the after edge.
    /// </summary>
    [PdfEnumValue("After")]
    After,

    /// <summary>
    /// Aligned with both edges.
    /// </summary>
    [PdfEnumValue("Justify")]
    Justify
}
