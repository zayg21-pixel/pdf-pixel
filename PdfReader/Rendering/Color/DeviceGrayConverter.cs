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

        public override unsafe void Sample8RgbaInPlace(byte* rgbaRow, int pixelCount, PdfRenderingIntent intent)
        {
            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                int baseIdx = pixelIndex * 4;
                byte g = rgbaRow[baseIdx];
                rgbaRow[baseIdx + 1] = g;
                rgbaRow[baseIdx + 2] = g;
                rgbaRow[baseIdx + 3] = 255; // set alpha to opaque
            }
        }
    }
}
