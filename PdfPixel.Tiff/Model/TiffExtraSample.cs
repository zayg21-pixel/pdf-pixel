namespace PdfPixel.Tiff.Model;

/// <summary>
/// Meaning of a sample beyond those the photometric interpretation defines (tag 338).
/// </summary>
public enum TiffExtraSample
{
    /// <summary>
    /// Unspecified data.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Alpha premultiplied into the color samples.
    /// </summary>
    AssociatedAlpha = 1,

    /// <summary>
    /// Alpha independent of the color samples.
    /// </summary>
    UnassociatedAlpha = 2
}
