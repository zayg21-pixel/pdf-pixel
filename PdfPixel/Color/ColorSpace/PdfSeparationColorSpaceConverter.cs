using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Models;

namespace PdfPixel.Color.ColorSpace;

internal sealed class PdfSeparationColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly PdfString? _name;
    private readonly PdfColorSpaceConverter _alternate;
    private readonly PdfFunction? _tintFunction;

    public PdfSeparationColorSpaceConverter(PdfString? name, PdfColorSpaceConverter? alternate, PdfFunction? tintFunction)
    {
        _name = name;
        _alternate = alternate ?? PdfDeviceGrayColorSpaceConverter.Instance;
        _tintFunction = tintFunction;
    }

    public override int Components => 1;

    public override bool IsDevice => false;

    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
    {
        ColorTransformSampler alternateSampler = _alternate.GetRgbaSampler(intent, postTransform, normalize: true);

        if (_tintFunction == null)
        {
            return alternateSampler;
        }

        return new ColorTransformSampler(alternateSampler.ColorTransform, _tintFunction.Evaluate);
    }
}
