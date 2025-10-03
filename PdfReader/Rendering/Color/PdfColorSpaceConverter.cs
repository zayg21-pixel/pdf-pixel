using SkiaSharp;
using PdfReader.Models;
using System;

namespace PdfReader.Rendering.Color
{
    public abstract class PdfColorSpaceConverter
    {
        public abstract int Components { get; }

        public abstract bool IsDevice { get; }

        public abstract SKColor ToSrgb(ReadOnlySpan<float> comps01, PdfRenderingIntent intent);

        protected static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        protected static byte ToByte(float v01) => (byte)(Clamp01(v01) * 255f + 0.5f);
    }
}
