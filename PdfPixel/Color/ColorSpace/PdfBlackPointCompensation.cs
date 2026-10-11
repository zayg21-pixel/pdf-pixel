using PdfPixel.Text;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Black point compensation setting (/UseBlackPtComp) of a graphics state parameter dictionary.
/// </summary>
[PdfEnum]
public enum PdfBlackPointCompensation
{
    /// <summary>
    /// Black point compensation is chosen by the PDF processor.
    /// </summary>
    [PdfEnumDefaultValue]
    [PdfEnumValue("Default")]
    Default,

    /// <summary>
    /// Black point compensation is applied.
    /// </summary>
    [PdfEnumValue("ON")]
    On,

    /// <summary>
    /// Black point compensation is not applied.
    /// </summary>
    [PdfEnumValue("OFF")]
    Off
}
