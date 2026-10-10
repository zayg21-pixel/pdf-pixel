using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Named value of a glyph orientation in vertical text (GlyphOrientationVertical).
/// </summary>
[PdfEnum]
public enum PdfStructureGlyphOrientationMode
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Orientation chosen by whether the text is fullwidth.
    /// </summary>
    [PdfEnumValue("Auto")]
    Auto
}
