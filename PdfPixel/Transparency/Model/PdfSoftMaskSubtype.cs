using PdfPixel.Text;

namespace PdfPixel.Transparency.Model;

/// <summary>
/// Enumeration of supported soft mask subtypes.
/// </summary>
[PdfEnum]
public enum PdfSoftMaskSubtype
{
    /// <summary>
    /// Unknown soft mask subtype (default).
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown = 0,

    /// <summary>
    /// Alpha soft mask subtype (/Alpha).
    /// </summary>
    [PdfEnumValue("Alpha")]
    Alpha,

    /// <summary>
    /// Luminosity soft mask subtype (/Luminosity).
    /// </summary>
    [PdfEnumValue("Luminosity")]
    Luminosity
}
