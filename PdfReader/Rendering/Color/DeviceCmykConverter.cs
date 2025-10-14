using PdfReader.Models;
using PdfReader.Rendering.Color.Clut;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Color
{
    internal sealed class DeviceCmykConverter : PdfColorSpaceConverter
    {
        private const int MaxByte = 255;

        public static readonly DeviceCmykConverter Instance = new DeviceCmykConverter();
        private static readonly IRgbaSampler _sampler = new CmykDeviceSampler();

        public override int Components => 4;

        public override bool IsDevice => true;

        internal override IRgbaSampler GetSampler(PdfRenderingIntent intent)
        {
            return _sampler;
        }

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

        private sealed class CmykDeviceSampler : IRgbaSampler
        {
            public bool IsDefault => false;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Sample(ref Rgba source, ref Rgba destination)
            {
                byte k = source.A;
                byte r = (byte)((MaxByte - source.R) * (MaxByte - k) >> 8);
                byte g = (byte)((MaxByte - source.G) * (MaxByte - k) >> 8);
                byte b = (byte)((MaxByte - source.B) * (MaxByte - k) >> 8);

                destination.R = r;
                destination.G = g;
                destination.B = b;
                destination.A = MaxByte;
            }
        }
    }
}
