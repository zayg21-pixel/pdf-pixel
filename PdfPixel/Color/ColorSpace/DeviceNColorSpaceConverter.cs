using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Models;
using System;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Simplified DeviceN color space converter.
/// Maps N-component tint array through a tint transform function into an alternate color space.
/// According to PDF spec, /DeviceN [/Names] /AltCS /TintTransform.
/// We currently only support non-PostScript tint functions (types 0,2,3) via PdfFunctions.
/// </summary>
internal sealed class DeviceNColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly PdfString[] _componentNames;
    private readonly PdfColorSpaceConverter _alternate;
    private readonly PdfFunction _tintFunction;

    public DeviceNColorSpaceConverter(PdfString[] componentNames, PdfColorSpaceConverter alternate, PdfFunction tintFunction)
    {
        _componentNames = componentNames ?? Array.Empty<PdfString>();
        _alternate = alternate ?? DeviceRgbConverter.Instance;
        _tintFunction = tintFunction;
    }

    public override int Components => _componentNames.Length > 0 ? _componentNames.Length : 1;

    public override bool IsDevice => false;

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform postTransform)
    {
        if (_tintFunction == null)
        {
            return _alternate.GetRgbaSampler(intent, postTransform);
        }

        return new FunctionSampler(_tintFunction, _alternate.GetRgbaSampler(intent, postTransform));
    }
}
