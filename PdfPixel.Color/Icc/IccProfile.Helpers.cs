namespace PdfPixel.Color.Icc;

/// <summary>
/// Partial ICC profile implementation: tag lookup helpers.
/// </summary>
public sealed partial class IccProfile
{
    /// <summary>
    /// Determines whether the profile has its own A2B LUT for the specified rendering intent.
    /// </summary>
    /// <param name="intent">The rendering intent.</param>
    /// <returns><c>true</c> if the intent's own A2B LUT is present; otherwise, <c>false</c>.</returns>
    public bool HasA2BLut(IccRenderingIntent intent) => GetA2BLut(intent) != null;

    /// <summary>
    /// Gets the profile's own A2B LUT pipeline for the specified rendering intent.
    /// </summary>
    /// <param name="intent">The rendering intent.</param>
    /// <returns>The A2B LUT pipeline, or null when the intent's own A2B LUT is absent.</returns>
    public IccLutPipeline? GetA2BLut(IccRenderingIntent intent)
    {
        switch (intent)
        {
            case IccRenderingIntent.Perceptual:
                {
                    return A2BLut0;
                }
            case IccRenderingIntent.RelativeColorimetric:
            case IccRenderingIntent.AbsoluteColorimetric:
                {
                    return A2BLut1;
                }
            case IccRenderingIntent.Saturation:
                {
                    return A2BLut2;
                }
            default:
                {
                    return null;
                }
        }
    }
}
