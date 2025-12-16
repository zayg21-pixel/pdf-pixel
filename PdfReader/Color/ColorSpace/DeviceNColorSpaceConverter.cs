using PdfReader.Color.Lut;
using PdfReader.Functions;
using PdfReader.Models;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.ColorSpace;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent intent)
    {
        ReadOnlySpan<float> mapped = comps01;
        if (_tintFunction != null)
        {
            mapped = _tintFunction.Evaluate(comps01);
            if (mapped == null || mapped.Length == 0)
            {
                mapped = comps01;
            }
        }

        return _alternate.ToSrgb(mapped, intent);
    }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        switch (Components)
        {
            case 1:
            {
                return OneDLutGray.Build(intent, ToSrgbCore);
            }
            case 3:
            {
                return ThreeDLut.Build(intent, ToSrgbCore);
            }
            default:
            {
                return base.GetRgbaSamplerCore(intent);
            }
        }
    }
}
