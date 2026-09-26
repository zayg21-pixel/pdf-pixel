namespace PdfPixel.Tiff.Model;

/// <summary>
/// Compression scheme of a TIFF image (tag 259).
/// </summary>
public enum TiffCompression
{
    /// <summary>
    /// Compression not determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// No compression; samples are packed into bytes.
    /// </summary>
    Uncompressed = 1,

    /// <summary>
    /// CCITT Group 3 1-D Modified Huffman run-length encoding, each row byte aligned.
    /// </summary>
    CcittRle = 2,

    /// <summary>
    /// CCITT T.4 bi-level encoding (Group 3 fax).
    /// </summary>
    CcittGroup3 = 3,

    /// <summary>
    /// CCITT T.6 bi-level encoding (Group 4 fax).
    /// </summary>
    CcittGroup4 = 4,

    /// <summary>
    /// Lempel-Ziv-Welch.
    /// </summary>
    Lzw = 5,

    /// <summary>
    /// Original TIFF 6.0 JPEG scheme, superseded by <see cref="Jpeg"/>.
    /// </summary>
    OldJpeg = 6,

    /// <summary>
    /// JPEG as defined by TIFF Technical Note 2.
    /// </summary>
    Jpeg = 7,

    /// <summary>
    /// Deflate with a zlib wrapper, under its registered code.
    /// </summary>
    AdobeDeflate = 8,

    /// <summary>
    /// Macintosh PackBits run-length encoding.
    /// </summary>
    PackBits = 32773,

    /// <summary>
    /// Aperio JPEG 2000 codestreams holding YCbCr samples.
    /// </summary>
    AperioJpeg2000YCbCr = 33003,

    /// <summary>
    /// Aperio JPEG 2000 codestreams holding RGB samples.
    /// </summary>
    AperioJpeg2000Rgb = 33005,

    /// <summary>
    /// Deflate with a zlib wrapper, under its original private code.
    /// </summary>
    Deflate = 32946,

    /// <summary>
    /// JPEG 2000 codestreams or JP2 files.
    /// </summary>
    Jpeg2000 = 34712
}
