using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// Enumerates the supported XObject subtypes for PDF resources.
/// </summary>
[PdfEnum]
public enum PdfXObjectSubtype
{
    /// <summary>
    /// Unrecognized or absent XObject subtype.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Form XObject subtype (/Form)
    /// </summary>
    [PdfEnumValue("Form")]
    Form,

    /// <summary>
    /// Image XObject subtype (/Image)
    /// </summary>
    [PdfEnumValue("Image")]
    Image,

    /// <summary>
    /// PostScript XObject subtype (/PS)
    /// </summary>
    [PdfEnumValue("PS")]
    PS,

    /// <summary>
    /// XML XObject subtype (/XML, PDF 1.6+)
    /// </summary>
    [PdfEnumValue("XML")]
    XML,

    /// <summary>
    /// Movie XObject subtype (/Movie, PDF 1.2+)
    /// </summary>
    [PdfEnumValue("Movie")]
    Movie,

    /// <summary>
    /// Sound XObject subtype (/Sound, PDF 1.2+)
    /// </summary>
    [PdfEnumValue("Sound")]
    Sound
}
