using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Specifies the visual effect applied to an annotation border.
/// </summary>
[PdfEnum]
public enum PdfBorderEffectType
{
    /// <summary>
    /// No effect — border is drawn normally.
    /// </summary>
    [PdfEnumDefaultValue]
    [PdfEnumValue("S")]
    Solid,

    /// <summary>
    /// Cloudy effect — border appears soft and bumpy.
    /// </summary>
    [PdfEnumValue("C")]
    Cloudy
}
