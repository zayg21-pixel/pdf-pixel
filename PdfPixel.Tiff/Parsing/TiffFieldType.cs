namespace PdfPixel.Tiff.Parsing;

/// <summary>
/// Data type of a TIFF directory entry (TIFF 6.0 Section 2, BigTIFF adds 16 to 18).
/// </summary>
internal enum TiffFieldType
{
    Byte = 1,
    Ascii = 2,
    Short = 3,
    Long = 4,
    Rational = 5,
    SignedByte = 6,
    Undefined = 7,
    SignedShort = 8,
    SignedLong = 9,
    SignedRational = 10,
    Float = 11,
    Double = 12,
    Ifd = 13,
    Long8 = 16,
    SignedLong8 = 17,
    Ifd8 = 18
}
