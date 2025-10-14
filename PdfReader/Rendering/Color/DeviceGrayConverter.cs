using PdfReader.Models;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Color
{
    internal sealed class DeviceGrayConverter : PdfColorSpaceConverter
    {
        public static readonly DeviceGrayConverter Instance = new DeviceGrayConverter();

        public override int Components => 1;

        public override bool IsDevice => true;

        protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
        {
            byte g = ToByte(comps01.Length > 0 ? comps01[0] : 0f);
            return new SKColor(g, g, g);
        }
    }
}
