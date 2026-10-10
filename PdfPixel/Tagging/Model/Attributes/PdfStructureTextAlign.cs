using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Inline alignment of text within lines (TextAlign).
/// </summary>
[PdfEnum]
public enum PdfStructureTextAlign
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Aligned with the start edge.
    /// </summary>
    [PdfEnumValue("Start")]
    Start,

    /// <summary>
    /// Centered between the start and end edges.
    /// </summary>
    [PdfEnumValue("Center")]
    Center,

    /// <summary>
    /// Aligned with the end edge.
    /// </summary>
    [PdfEnumValue("End")]
    End,

    /// <summary>
    /// Aligned with both edges.
    /// </summary>
    [PdfEnumValue("Justify")]
    Justify
}
