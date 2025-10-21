using PdfReader.Models;
using PdfReader.Rendering.Color.Clut;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Color
{
    internal sealed class DeviceCmykConverter : PdfColorSpaceConverter
    {
        public static readonly DeviceCmykConverter Instance = new DeviceCmykConverter();

        public override int Components => 4;

        public override bool IsDevice => true;

        protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
        {
            float c = comps01.Length > 0 ? Clamp01(comps01[0]) : 0f;
            float m = comps01.Length > 1 ? Clamp01(comps01[1]) : 0f;
            float y = comps01.Length > 2 ? Clamp01(comps01[2]) : 0f;
            float k = comps01.Length > 3 ? Clamp01(comps01[3]) : 0f;

            float r01 = (1f - c) * (1f - k);
            float g01 = (1f - m) * (1f - k);
            float b01 = (1f - y) * (1f - k);

            return new SKColor(ToByte(r01), ToByte(g01), ToByte(b01));
        }

        protected override SKColorFilter BuldColorFilter(PdfRenderingIntent intent)
        {
            return DeviceCmykColorFilter.BuildDeviceCmykColorFilter();
        }
    }
}
