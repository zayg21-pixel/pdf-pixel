using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Enumerates the supported inline image filter abbreviations in PDF content streams.
/// </summary>
[PdfEnum]
public enum PdfInlineImageFilter
{
    /// <summary>
    /// Unrecognized filter name.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Flate (zlib/deflate) compression (<c>Fl</c>).
    /// </summary>
    [PdfEnumValue("Fl")]
    Flate,

    /// <summary>
    /// LZW compression (<c>LZW</c>).
    /// </summary>
    [PdfEnumValue("LZW")]
    LZW,

    /// <summary>
    /// ASCII hexadecimal encoding (<c>AHx</c>).
    /// </summary>
    [PdfEnumValue("AHx")]
    ASCIIHex,

    /// <summary>
    /// ASCII base-85 encoding (<c>A85</c>).
    /// </summary>
    [PdfEnumValue("A85")]
    ASCII85,

    /// <summary>
    /// Run-length encoding (<c>RL</c>).
    /// </summary>
    [PdfEnumValue("RL")]
    RunLength,

    /// <summary>
    /// CCITT facsimile (Group 3/4) compression (<c>CCF</c>).
    /// </summary>
    [PdfEnumValue("CCF")]
    CCITTFax,

    /// <summary>
    /// DCT (JPEG) lossy compression (<c>DCT</c>).
    /// </summary>
    [PdfEnumValue("DCT")]
    DCT,

    /// <summary>
    /// JPEG 2000 compression (<c>JPX</c>).
    /// </summary>
    [PdfEnumValue("JPX")]
    JPX,

    /// <summary>
    /// JBIG2 bi-level image compression (<c>JB2</c>).
    /// </summary>
    [PdfEnumValue("JB2")]
    JBIG2
}
