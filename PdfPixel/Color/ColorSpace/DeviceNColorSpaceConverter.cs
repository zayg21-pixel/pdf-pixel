using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Models;
using System;

namespace PdfPixel.Color.ColorSpace;

internal sealed class DeviceNColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly PdfString?[] _componentNames;
    private readonly PdfColorSpaceConverter _alternate;
    private readonly PdfFunction? _tintFunction;

    public DeviceNColorSpaceConverter(PdfString?[]? componentNames, PdfColorSpaceConverter? alternate, PdfFunction? tintFunction)
    {
        _componentNames = componentNames ?? Array.Empty<PdfString?>();
        _alternate = alternate ?? DeviceRgbConverter.Instance;
        _tintFunction = tintFunction;
    }

    public override int Components => (_componentNames.Length > 0) ? _componentNames.Length : 1;

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
