using PdfRender.Color.Sampling;
using PdfRender.Color.Transform;

namespace PdfRender.Color.ColorSpace;

/// <summary>
/// Static converter for DeviceRGB color space.
/// </summary>
internal sealed class DeviceRgbConverter : PdfColorSpaceConverter
{
    public static readonly DeviceRgbConverter Instance = new DeviceRgbConverter();

    public override int Components => 3;
    public override bool IsDevice => true;

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform postTransform)
    {
        var chained = new ChainedColorTransform(postTransform);
        return new ColorTransformSampler(chained);
    }
}
