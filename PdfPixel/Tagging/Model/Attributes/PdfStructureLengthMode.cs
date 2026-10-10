using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Named value of a layout length (Width, Height, LineHeight).
/// </summary>
[PdfEnum]
public enum PdfStructureLengthMode
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// No specific constraint; determined by the content.
    /// </summary>
    [PdfEnumValue("Auto")]
    Auto,

    /// <summary>
    /// Line height adjusted to include any baseline shift (LineHeight only).
    /// </summary>
    [PdfEnumValue("Normal")]
    Normal
}
