using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents line ending styles for line annotations.
/// </summary>
[PdfEnum]
public enum PdfLineEndingStyle
{
    /// <summary>
    /// No line ending.
    /// </summary>
    [PdfEnumValue("None")]
    [PdfEnumDefaultValue]
    None,

    /// <summary>
    /// Square line ending.
    /// </summary>
    [PdfEnumValue("Square")]
    Square,

    /// <summary>
    /// Circle line ending.
    /// </summary>
    [PdfEnumValue("Circle")]
    Circle,

    /// <summary>
    /// Diamond line ending.
    /// </summary>
    [PdfEnumValue("Diamond")]
    Diamond,

    /// <summary>
    /// Open arrow line ending.
    /// </summary>
    [PdfEnumValue("OpenArrow")]
    OpenArrow,

    /// <summary>
    /// Closed arrow line ending.
    /// </summary>
    [PdfEnumValue("ClosedArrow")]
    ClosedArrow,

    /// <summary>
    /// Butt line ending (perpendicular line at endpoint).
    /// </summary>
    [PdfEnumValue("Butt")]
    Butt,

    /// <summary>
    /// Right open arrow line ending.
    /// </summary>
    [PdfEnumValue("ROpenArrow")]
    ROpenArrow,

    /// <summary>
    /// Right closed arrow line ending.
    /// </summary>
    [PdfEnumValue("RClosedArrow")]
    RClosedArrow,

    /// <summary>
    /// Slash line ending.
    /// </summary>
    [PdfEnumValue("Slash")]
    Slash
}
