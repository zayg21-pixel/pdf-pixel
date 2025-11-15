using PdfReader.Models;
using SkiaSharp;
using System;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Static converter for DeviceRGB color space.
/// </summary>
internal sealed class DeviceRgbConverter : PdfColorSpaceConverter
{
    public static readonly DeviceRgbConverter Instance = new DeviceRgbConverter();

    public override int Components => 3;
    public override bool IsDevice => true;

    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
    {
        byte r = ToByte(comps01.Length > 0 ? comps01[0] : 0f);
        byte g = ToByte(comps01.Length > 1 ? comps01[1] : 0f);
        byte b = ToByte(comps01.Length > 2 ? comps01[2] : 0f);
        return new SKColor(r, g, b);
    }

    public override SKColorFilter AsColorFilter(PdfRenderingIntent intent)
    {
        return null;
    }
}
