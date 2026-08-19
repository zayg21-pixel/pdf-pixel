using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The DeviceRGB color space: three components giving the red, green, and blue additive primaries
/// of the output device.
/// </summary>
public sealed class PdfDeviceRgbColorSpaceConverter : PdfColorSpaceConverter
{
    /// <summary>
    /// The shared instance of this color space.
    /// </summary>
    public static readonly PdfDeviceRgbColorSpaceConverter Instance = new();

    /// <inheritdoc />
    public override int Components => 3;

    /// <inheritdoc />
    public override bool IsDevice => true;

    /// <inheritdoc />
    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
    {
        ChainedColorTransform chained = new(postTransform);
        return new ColorTransformSampler(chained);
    }
}
