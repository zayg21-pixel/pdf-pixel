using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Justification of the lines within a ruby assembly (RubyAlign).
/// </summary>
[PdfEnum]
public enum PdfStructureRubyAlign
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Aligned on the start edge.
    /// </summary>
    [PdfEnumValue("Start")]
    Start,

    /// <summary>
    /// Centered.
    /// </summary>
    [PdfEnumValue("Center")]
    Center,

    /// <summary>
    /// Aligned on the end edge.
    /// </summary>
    [PdfEnumValue("End")]
    End,

    /// <summary>
    /// Expanded to fill the available width.
    /// </summary>
    [PdfEnumValue("Justify")]
    Justify,

    /// <summary>
    /// Expanded to fill the available width, with space also at the start and end edges.
    /// </summary>
    [PdfEnumValue("Distribute")]
    Distribute
}
