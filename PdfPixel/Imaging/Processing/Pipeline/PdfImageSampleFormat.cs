namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// How the samples of a <see cref="PdfImageSamples"/> are stored.
/// </summary>
public enum PdfImageSampleFormat
{
    /// <summary>
    /// Unsigned integers, packed at the bit depth of the samples.
    /// </summary>
    Integer,

    /// <summary>
    /// 32-bit floats.
    /// </summary>
    Float
}
