using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Direction of a step of an embedded go-to path (/R).
/// </summary>
[PdfEnum]
public enum PdfEmbeddedPathDirection
{
    /// <summary>
    /// Unknown or unsupported direction.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// To the document the current document is embedded in.
    /// </summary>
    [PdfEnumValue("P")]
    Parent,

    /// <summary>
    /// To a document embedded in the current document.
    /// </summary>
    [PdfEnumValue("C")]
    Child
}
