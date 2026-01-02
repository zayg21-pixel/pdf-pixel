using PdfReader.Color.Sampling;
using PdfReader.Color.Transform;
using System.Numerics;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Static converter for the Device Gray color space to sRGB.
/// </summary>
internal sealed class DeviceGrayConverter : PdfColorSpaceConverter
{
    public static readonly DeviceGrayConverter Instance = new DeviceGrayConverter();

    public override int Components => 1;

    public override bool IsDevice => true;

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform postTransform)
    {
        var chained = new ChainedColorTransform(new FunctionColorTransform(x => new Vector4(x.X, x.X, x.X, 1f)), postTransform);
        return new ColorTransformSampler(chained);
    }
}
