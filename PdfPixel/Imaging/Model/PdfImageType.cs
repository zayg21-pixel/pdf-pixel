namespace PdfPixel.Imaging.Model;

/// <summary>
/// Simplified image type classification derived from /Filter.
/// </summary>
public enum PdfImageType
{
    /// <summary>Non image-specific encoding (e.g., Flate/LZW/ASCII...).</summary>
    Raw = 0,
    /// <summary>JPEG (DCTDecode).</summary>
    JPEG,
    /// <summary>JPEG 2000 (JPXDecode).</summary>
    JPEG2000,
    /// <summary>CCITT Fax (CCITTFaxDecode).</summary>
    CCITT,
    /// <summary>JBIG2 bi-tonal (JBIG2Decode).</summary>
    JBIG2
}