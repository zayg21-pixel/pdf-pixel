using PdfPixel.Text;

namespace PdfPixel.Streams;

/// <summary>
/// Identifies the stream filter applied to a PDF stream object.
/// </summary>
[PdfEnum]
public enum PdfFilterType
{
    /// <summary>
    /// Unrecognized or absent filter name.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Flate (zlib/deflate) compression.
    /// </summary>
    [PdfEnumValue("FlateDecode")]
    FlateDecode,

    /// <summary>
    /// LZW compression.
    /// </summary>
    [PdfEnumValue("LZWDecode")]
    LZWDecode,

    /// <summary>
    /// DCT (JPEG) lossy compression.
    /// </summary>
    [PdfEnumValue("DCTDecode")]
    DCTDecode,

    /// <summary>
    /// ASCII hexadecimal encoding.
    /// </summary>
    [PdfEnumValue("ASCIIHexDecode")]
    ASCIIHexDecode,

    /// <summary>
    /// ASCII base-85 encoding.
    /// </summary>
    [PdfEnumValue("ASCII85Decode")]
    ASCII85Decode,

    /// <summary>
    /// JPEG 2000 compression.
    /// </summary>
    [PdfEnumValue("JPXDecode")]
    JPXDecode,

    /// <summary>
    /// JBIG2 bi-level image compression.
    /// </summary>
    [PdfEnumValue("JBIG2Decode")]
    JBIG2Decode,

    /// <summary>
    /// CCITT facsimile (Group 3/4) compression.
    /// </summary>
    [PdfEnumValue("CCITTFaxDecode")]
    CCITTFaxDecode,

    /// <summary>
    /// Run-length encoding.
    /// </summary>
    [PdfEnumValue("RunLengthDecode")]
    RunLengthDecode,

    /// <summary>
    /// Crypt filter; delegates decryption to the document's security handler.
    /// </summary>
    [PdfEnumValue("Crypt")]
    Crypt
}
