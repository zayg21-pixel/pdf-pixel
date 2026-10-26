namespace PdfPixel.Png;

/// <summary>
/// Unit of the pixel dimensions of a PNG image (pHYs unit specifier).
/// </summary>
public enum PngUnit
{
    /// <summary>
    /// No unit: the dimensions give the pixel aspect ratio only.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Pixels per meter.
    /// </summary>
    Meter = 1
}
