using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;

namespace PdfPixel.Color.ColorSpace;

internal sealed class PdfPatternColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly PdfColorSpaceConverter? _baseColorSpace;

    public PdfPatternColorSpaceConverter(PdfColorSpaceConverter? baseColorSpace) => _baseColorSpace = baseColorSpace;

    public override bool IsDevice => false;

    public override int Components => _baseColorSpace?.Components ?? 0;

    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
        => _baseColorSpace?.GetRgbaSampler(intent, postTransform, normalize) ?? new ColorTransformSampler(new ChainedColorTransform(postTransform));
}
