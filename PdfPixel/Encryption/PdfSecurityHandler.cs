using PdfPixel.Text;

namespace PdfPixel.Encryption;

/// <summary>
/// Security handlers (/Filter) of an encrypted document.
/// </summary>
[PdfEnum]
public enum PdfSecurityHandler
{
    /// <summary>
    /// Unknown or unsupported security handler.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Standard password-based security handler.
    /// </summary>
    [PdfEnumValue("Standard")]
    Standard
}
