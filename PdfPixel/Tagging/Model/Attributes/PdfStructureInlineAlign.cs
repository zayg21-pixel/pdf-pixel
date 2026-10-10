using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Inline-progression alignment of content within a table cell (InlineAlign).
/// </summary>
[PdfEnum]
public enum PdfStructureInlineAlign
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
    End
}
