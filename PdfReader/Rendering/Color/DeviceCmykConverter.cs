using SkiaSharp;
using System;
using PdfReader.Models;

namespace PdfReader.Rendering.Color
{
    internal sealed class DeviceCmykConverter : PdfColorSpaceConverter
    {
        // Singleton naive CMYK converter (no ICC color management)
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

        public override unsafe void Sample8RgbaInPlace(byte* rgbaRow, int pixelCount, PdfRenderingIntent intent)
        {
            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                int baseIdx = pixelIndex * 4;
                float c = ToFloat01(rgbaRow[baseIdx]);
                float m = ToFloat01(rgbaRow[baseIdx + 1]);
                float y = ToFloat01(rgbaRow[baseIdx + 2]);
                float k = ToFloat01(rgbaRow[baseIdx + 3]);
                float invK = 1f - k;
                
                byte r = ToByte((1f - c) * invK);
                byte g = ToByte((1f - m) * invK);
                byte b = ToByte((1f - y) * invK);
                rgbaRow[baseIdx] = r;
                rgbaRow[baseIdx + 1] = g;
                rgbaRow[baseIdx + 2] = b;
                rgbaRow[baseIdx + 3] = 255;
            }
        }
    }
}
