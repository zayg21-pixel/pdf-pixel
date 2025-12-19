using PdfReader.Color.Sampling;
using PdfReader.Color.Transform;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Static converter for DeviceRGB color space.
/// </summary>
internal sealed class DeviceRgbConverter : PdfColorSpaceConverter
{
    public static readonly DeviceRgbConverter Instance = new DeviceRgbConverter();

    public override int Components => 3;
    public override bool IsDevice => true;

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        // no-op transform
        var chained = new ChainedColorTransform();
        return new ColorTransformSampler(chained);
    }
}
