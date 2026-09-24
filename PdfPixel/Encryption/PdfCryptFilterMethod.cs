using PdfPixel.Text;

namespace PdfPixel.Encryption;

/// <summary>
/// Crypt filter methods (/CFM) as defined by the PDF specification.
/// </summary>
[PdfEnum]
public enum PdfCryptFilterMethod
{
    /// <summary>
    /// No decryption.
    /// </summary>
    [PdfEnumDefaultValue]
    [PdfEnumValue("None")]
    None,

    /// <summary>
    /// RC4 decryption.
    /// </summary>
    [PdfEnumValue("V2")]
    V2,

    /// <summary>
    /// AES-128 decryption in CBC mode.
    /// </summary>
    [PdfEnumValue("AESV2")]
    AESV2,

    /// <summary>
    /// AES-256 decryption in CBC mode.
    /// </summary>
    [PdfEnumValue("AESV3")]
    AESV3
}
