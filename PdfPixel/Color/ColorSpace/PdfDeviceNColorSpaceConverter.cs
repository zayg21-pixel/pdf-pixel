using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Models;
using System;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The DeviceN color space: color values are tints of several named colorants, one per component.
/// </summary>
public sealed class PdfDeviceNColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly PdfString?[] _componentNames;
    private readonly PdfColorSpaceConverter _alternate;
    private readonly PdfFunction? _tintFunction;

    /// <summary>
    /// Initializes the space from its colorant names, alternate color space, and tint transform function.
    /// </summary>
    public PdfDeviceNColorSpaceConverter(PdfString?[]? componentNames, PdfColorSpaceConverter? alternate, PdfFunction? tintFunction)
    {
        _componentNames = componentNames ?? Array.Empty<PdfString?>();
        _alternate = alternate ?? PdfDeviceRgbColorSpaceConverter.Instance;
        _tintFunction = tintFunction;
    }

    /// <inheritdoc />
    public override int Components => (_componentNames.Length > 0) ? _componentNames.Length : 1;

    /// <inheritdoc />
    public override bool IsDevice => false;

    /// <inheritdoc />
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
