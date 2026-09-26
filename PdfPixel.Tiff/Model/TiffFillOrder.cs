namespace PdfPixel.Tiff.Model;

/// <summary>
/// Bit order of the compressed data within each byte (tag 266).
/// </summary>
public enum TiffFillOrder
{
    /// <summary>
    /// Fill order not determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The first bit is the most significant bit of the byte.
    /// </summary>
    MostSignificantBitFirst = 1,

    /// <summary>
    /// The first bit is the least significant bit of the byte.
    /// </summary>
    LeastSignificantBitFirst = 2
}
