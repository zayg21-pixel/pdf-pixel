using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Placement of ruby text relative to the ruby base (RubyPosition).
/// </summary>
[PdfEnum]
public enum PdfStructureRubyPosition
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Along the before edge.
    /// </summary>
    [PdfEnumValue("Before")]
    Before,

    /// <summary>
    /// Along the after edge.
    /// </summary>
    [PdfEnumValue("After")]
    After,

    /// <summary>
    /// Formatted as a warichu following the ruby base.
    /// </summary>
    [PdfEnumValue("Warichu")]
    Warichu,

    /// <summary>
    /// Formatted as a parenthesis comment following the ruby base.
    /// </summary>
    [PdfEnumValue("Inline")]
    Inline
}
