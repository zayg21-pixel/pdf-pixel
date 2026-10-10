using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Positioning of a structure element (Placement).
/// </summary>
[PdfEnum]
public enum PdfStructurePlacement
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Stacked in the block-progression direction.
    /// </summary>
    [PdfEnumValue("Block")]
    Block,

    /// <summary>
    /// Packed in the inline-progression direction.
    /// </summary>
    [PdfEnumValue("Inline")]
    Inline,

    /// <summary>
    /// Floated to the before edge of the reference area.
    /// </summary>
    [PdfEnumValue("Before")]
    Before,

    /// <summary>
    /// Floated to the start edge of the reference area.
    /// </summary>
    [PdfEnumValue("Start")]
    Start,

    /// <summary>
    /// Floated to the end edge of the reference area.
    /// </summary>
    [PdfEnumValue("End")]
    End
}
