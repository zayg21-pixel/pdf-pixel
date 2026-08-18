using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

internal sealed class PdfDeviceRgbColorSpaceConverter : PdfColorSpaceConverter
{
    public static readonly PdfDeviceRgbColorSpaceConverter Instance = new();

    public override int Components => 3;
    public override bool IsDevice => true;

    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
    {
        ChainedColorTransform chained = new(postTransform);
        return new ColorTransformSampler(chained);
    }
}
