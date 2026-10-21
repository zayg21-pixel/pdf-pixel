using PdfPixel.Text;

namespace PdfPixel.Measures.Model;

/// <summary>
/// Position of the unit label relative to a formatted measurement value (/O).
/// </summary>
[PdfEnum]
public enum PdfNumberLabelPosition
{
    /// <summary>
    /// The label follows the value.
    /// </summary>
    [PdfEnumDefaultValue]
    [PdfEnumValue("S")]
    Suffix,

    /// <summary>
    /// The label precedes the value.
    /// </summary>
    [PdfEnumValue("P")]
    Prefix
}
