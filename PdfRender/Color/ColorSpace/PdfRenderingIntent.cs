using PdfRender.Text;

namespace PdfRender.Color.ColorSpace;

/// <summary>
/// PDF rendering intents as defined in PDF specification section 11.7.5
/// Controls how colors are mapped between different color spaces and devices
/// </summary>
[PdfEnum]
public enum PdfRenderingIntent
{
    /// <summary>
    /// RelativeColorimetric intent - preserves color accuracy within the target gamut.
    /// Out-of-gamut colors are mapped to the closest reproducible color.
    /// Preserves the white point relationship between source and destination.
    /// Best for images where color accuracy is important but some gamut compression is acceptable.
    /// </summary>
    [PdfEnumDefaultValue]
    [PdfEnumValue("RelativeColorimetric")]
    RelativeColorimetric,
    /// <summary>
    /// AbsoluteColorimetric intent - preserves exact color values.
    /// Maintains absolute color accuracy without white point adaptation.
    /// Best for proofing applications where exact color reproduction is critical.
    /// </summary>
    [PdfEnumValue("AbsoluteColorimetric")]
    AbsoluteColorimetric,
    /// <summary>
    /// Perceptual intent - optimizes for overall image appearance.
    /// Compresses the entire source gamut to fit the destination gamut.
    /// Best for photographic images where overall appearance matters more than exact colors.
    /// </summary>
    [PdfEnumValue("Perceptual")]
    Perceptual,
    /// <summary>
    /// Saturation intent - optimizes for vivid colors.
    /// Preserves color saturation at the expense of color accuracy.
    /// Best for business graphics where vivid, saturated colors are desired.
    /// </summary>
    [PdfEnumValue("Saturation")]
    Saturation
}