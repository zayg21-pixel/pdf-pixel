namespace PdfPixel.Color.Icc.Model;

/// <summary>
/// Rendering intent defined in <see cref="IccProfileHeader"/>.
/// </summary>
public enum IccRenderingIntent
{
    /// <summary>
    /// Perceptual intent.
    /// </summary>
    Perceptual = 0,

    /// <summary>
    /// Relative colorimetric intent.
    /// </summary>
    RelativeColorimetric = 1,

    /// <summary>
    /// Saturation intent.
    /// </summary>
    Saturation = 2,

    /// <summary>
    /// Absolute colorimetric intent.
    /// </summary>
    AbsoluteColorimetric = 3
}
