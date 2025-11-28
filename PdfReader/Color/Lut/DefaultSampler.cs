using PdfReader.Color.ColorSpace;
using PdfReader.Color.Filters;
using PdfReader.Color.Structures;
using SkiaSharp;
using System;

namespace PdfReader.Color.Lut;

/// <summary>
/// Represents the default RGBA sampler that converts colors using a specified color space converter and rendering intent.
/// </summary>
internal class DefaultSampler : IRgbaSampler
{
    private readonly DeviceToSrgbCore _colorSpaceConverter;
    private readonly PdfRenderingIntent _intent;

    public DefaultSampler(PdfRenderingIntent intent, DeviceToSrgbCore converter)
    {
        _colorSpaceConverter = converter ?? throw new ArgumentNullException(nameof(converter));
        _intent = intent;
    }

    public bool IsDefault => true;

    public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
    {
        var result = _colorSpaceConverter(source, _intent);
        destination = new RgbaPacked(result.Red, result.Green, result.Blue, result.Alpha);
    }

    public SKColor SampleColor(ReadOnlySpan<float> source)
    {
        return _colorSpaceConverter(source, _intent);
    }
}
