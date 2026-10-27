using PdfPixel.Objects;

namespace PdfPixel.Annotations;

/// <summary>
/// Represents the highlighting mode for link annotations.
/// </summary>
[PdfEnum]
public enum PdfLinkHighlightMode
{
    /// <summary>
    /// Invert the colors of the annotation rectangle (default for PDF 1.2+).
    /// </summary>
    [PdfEnumValue("I")]
    [PdfEnumDefaultValue]
    Invert,

    /// <summary>
    /// No highlighting.
    /// </summary>
    [PdfEnumValue("N")]
    None,

    /// <summary>
    /// Invert the border of the annotation rectangle.
    /// </summary>
    [PdfEnumValue("O")]
    Outline,

    /// <summary>
    /// Display the annotation as if it were being pushed below the surface of the page.
    /// </summary>
    [PdfEnumValue("P")]
    Push
}
