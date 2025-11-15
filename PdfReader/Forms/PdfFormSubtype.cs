using PdfReader.Text;

namespace PdfReader.Forms;

/// <summary>
/// Enumerates the supported XObject subtypes for PDF resources.
/// </summary>
[PdfEnum]
public enum PdfXObjectSubtype
{
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
