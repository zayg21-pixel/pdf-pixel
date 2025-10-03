using PdfReader.Models;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Color
{
    internal sealed class DeviceRgbConverter : PdfColorSpaceConverter
    {
        public static readonly DeviceRgbConverter Instance = new DeviceRgbConverter();

        public override int Components => 3;

        public override bool IsDevice => true;

        public override SKColor ToSrgb(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
        {
            var r = ToByte(comps01.Length > 0 ? comps01[0] : 0f);
            var g = ToByte(comps01.Length > 1 ? comps01[1] : 0f);
            var b = ToByte(comps01.Length > 2 ? comps01[2] : 0f);
            return new SKColor(r, g, b);
        }
    }
}
