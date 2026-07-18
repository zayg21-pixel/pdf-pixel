using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Static converter for DeviceRGB color space.
/// </summary>
internal sealed class DeviceRgbConverter : PdfColorSpaceConverter
{
    public static readonly DeviceRgbConverter Instance = new();

    public override int Components => 3;
    public override bool IsDevice => true;

    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform? postTransform, bool normalize)
    {
        ChainedColorTransform chained = new(postTransform);
        return new ColorTransformSampler(chained);
    }
}
