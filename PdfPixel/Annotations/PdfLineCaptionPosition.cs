using PdfPixel.Objects;

namespace PdfPixel.Annotations;

/// <summary>
/// Position of a line annotation's caption relative to the line.
/// </summary>
[PdfEnum]
public enum PdfLineCaptionPosition
{
    /// <summary>
    /// Caption centered inside the line.
    /// </summary>
    [PdfEnumDefaultValue]
    [PdfEnumValue("Inline")]
    Inline,

    /// <summary>
    /// Caption on top of the line.
    /// </summary>
    [PdfEnumValue("Top")]
    Top
}
