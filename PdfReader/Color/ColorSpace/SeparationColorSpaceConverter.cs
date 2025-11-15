using System;
using SkiaSharp;
using PdfReader.Models;
using PdfReader.Functions;

namespace PdfReader.Color.ColorSpace;

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

    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent intent)
    {
        float tint = comps01.Length > 0 ? comps01[0] : 0f;
        ReadOnlySpan<float> mapped = comps01;

        if (_tintFunction != null)
        {
            mapped = _tintFunction.Evaluate(tint);
            if (mapped == null || mapped.Length == 0)
            {
                mapped = comps01;
            }
        }

        return _alternate.ToSrgb(mapped, intent);
    }
}
