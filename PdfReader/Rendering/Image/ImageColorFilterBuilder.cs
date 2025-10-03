using SkiaSharp;
using PdfReader.Models;
using PdfReader.Rendering.Color;
using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Image
{
    public static class ImageColorFilterBuilder
    {
        // Build a per-channel LUT color filter from a PDF Decode array when components are 8-bit.
        // Supports DeviceGray and DeviceRGB only. Other color spaces should use the bitmap pipeline.
        public static SKColorFilter TryCreateDecodeColorFilter(PdfImage pdfImage)
        {
            // Only apply on DeviceGray/DeviceRGB
            var colorSpace = pdfImage.ColorSpaceConverter;

            if (colorSpace is not DeviceRgbConverter && colorSpace is not DeviceGrayConverter)
                return null;

            var decode = pdfImage.DecodeArray;
            if (decode == null || decode.Count == 0) return null;
            if (pdfImage.BitsPerComponent != 8) return null;

            int components = colorSpace.Components;

            var luts = BuildLutsFromDecodeArray(decode, components);
            if (!luts.HasValue) return null;
            var (r, g, b, a) = luts.Value;

            // Skia expects ARGB order for CreateTable: (A, R, G, B)
            return SKColorFilter.CreateTable(a, r, g, b);
        }

        private static (byte[] r, byte[] g, byte[] b, byte[] a)? BuildLutsFromDecodeArray(IReadOnlyList<float> decode, int components)
        {
            try
            {
                // Decode array has 2 values per component: [d0 d1] for each component
                int expected = components * 2;
                if (decode.Count < expected) return null;

                var r = new byte[256];
                var g = new byte[256];
                var b = new byte[256];
                var a = new byte[256];
                for (int i = 0; i < 256; i++) a[i] = 255; // keep alpha

                // Helper to build a single channel LUT from d0,d1
                Func<float, float, byte[]> build = (d0, d1) =>
                {
                    var lut = new byte[256];
                    // Map x from [0..255] to [d0..d1] scaled to 0..255
                    for (int v = 0; v < 256; v++)
                    {
                        float t = v / 255f;
                        float outVal = d0 + t * (d1 - d0);
                        int u = (int)Math.Round(Math.Max(0f, Math.Min(1f, outVal)) * 255f);
                        lut[v] = (byte)u;
                    }
                    return lut;
                };

                if (components == 1)
                {
                    float d0 = decode[0];
                    float d1 = decode[1];
                    var lut = build(d0, d1);
                    Array.Copy(lut, r, 256);
                    Array.Copy(lut, g, 256);
                    Array.Copy(lut, b, 256);
                }
                else if (components == 3)
                {
                    float r0 = decode[0]; float r1 = decode[1];
                    float g0 = decode[2]; float g1 = decode[3];
                    float b0 = decode[4]; float b1 = decode[5];
                    r = build(r0, r1);
                    g = build(g0, g1);
                    b = build(b0, b1);
                }

                return (r, g, b, a);
            }
            catch
            {
                return null;
            }
        }
    }
}
