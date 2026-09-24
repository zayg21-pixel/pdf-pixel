using PdfPixel.Text;

namespace PdfPixel.Encryption;

/// <summary>
/// Moments at which a crypt filter requires authentication (/AuthEvent) as defined by the PDF specification.
/// </summary>
[PdfEnum]
public enum PdfAuthEvent
{
    /// <summary>
    /// Authentication when the document is opened.
    /// </summary>
    [PdfEnumDefaultValue]
    [PdfEnumValue("DocOpen")]
    DocumentOpen,

    /// <summary>
    /// Authentication when an embedded file is accessed.
    /// </summary>
    [PdfEnumValue("EFOpen")]
    EmbeddedFileOpen
}
