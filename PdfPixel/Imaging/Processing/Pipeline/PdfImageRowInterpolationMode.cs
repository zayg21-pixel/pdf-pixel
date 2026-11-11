namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// How <see cref="PdfImageRowInterpolationStep"/> interpolates along the axes that grow.
/// </summary>
public enum PdfImageRowInterpolationMode
{
    /// <summary>
    /// Every destination sample is the linear blend of the two source samples around its center.
    /// </summary>
    Linear,

    /// <summary>
    /// Every destination sample is the mean of the source samples its cell covers, each weighted by the share of the
    /// cell it covers, so that only cells crossing a sample boundary blend two samples.
    /// </summary>
    Area
}
