namespace PdfPixel.Tiff.Model;

/// <summary>
/// Numeric interpretation of the samples in a TIFF image (tag 339).
/// </summary>
public enum TiffSampleFormat
{
    /// <summary>
    /// Sample format not determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Unsigned integer.
    /// </summary>
    UnsignedInteger = 1,

    /// <summary>
    /// Two's complement signed integer.
    /// </summary>
    SignedInteger = 2,

    /// <summary>
    /// IEEE floating point.
    /// </summary>
    FloatingPoint = 3,

    /// <summary>
    /// Undefined data format.
    /// </summary>
    Undefined = 4
}
