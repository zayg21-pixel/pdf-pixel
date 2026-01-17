using PdfRender.Color.Sampling;
using PdfRender.Color.Transform;
using PdfRender.Functions;
using PdfRender.Models;

namespace PdfRender.Color.ColorSpace;

/// <summary>
/// Simplified Separation color space converter.
/// Maps single-component tint value through a tint transform function into a base color space.
/// If no function/base is present falls back to DeviceGray.
/// </summary>
internal sealed class SeparationColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly PdfString _name;
    private readonly PdfColorSpaceConverter _alternate;
    private readonly PdfFunction _tintFunction;

    public SeparationColorSpaceConverter(PdfString name, PdfColorSpaceConverter alternate, PdfFunction tintFunction)
    {
        _name = name;
        _alternate = alternate ?? DeviceGrayConverter.Instance;
        _tintFunction = tintFunction;
    }

    public override int Components => 1;

    public override bool IsDevice => false;

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform postTransform)
    {
        var alternateSampler = _alternate.GetRgbaSampler(intent, postTransform);

        if (_tintFunction == null)
        {
            return alternateSampler;
        }

        return new FunctionSampler(_tintFunction, _alternate.GetRgbaSampler(intent, postTransform));
    }
}
