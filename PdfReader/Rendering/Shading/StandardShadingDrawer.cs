using System;
using System.Collections.Generic;
using SkiaSharp;
using PdfReader.Models;
using PdfReader.Rendering.Color;

namespace PdfReader.Rendering.Shading
{
    /// <summary>
    /// Draws axial (type 2) and radial (type 3) shadings (function based and simple interpolation).
    /// Implements 1D sampled (type 0) function decoding for gradient color evaluation.
    /// </summary>
    public class StandardShadingDrawer : IShadingDrawer
    {
        public void DrawShading(SKCanvas canvas, PdfDictionary shading, PdfGraphicsState state, PdfPage page)
        {
            if (shading == null)
            {
                return;
            }

            int type = shading.GetInteger(PdfTokens.ShadingTypeKey);
            switch (type)
            {
                case 2:
                    DrawAxialShading(shading, state, canvas, page);
                    break;
                case 3:
                    DrawRadialShading(shading, state, canvas, page);
                    break;
                default:
                    Console.WriteLine("Shading type " + type + " not implemented");
                    break;
            }
        }

        private static void DrawAxialShading(PdfDictionary shading, PdfGraphicsState gs, SKCanvas canvas, PdfPage page)
        {
            var coords = shading.GetArray(PdfTokens.CoordsKey);
            if (coords == null || coords.Count < 4)
            {
                return;
            }

            float x0 = coords[0].AsFloat();
            float y0 = coords[1].AsFloat();
            float x1 = coords[2].AsFloat();
            float y1 = coords[3].AsFloat();

            GetExtend(shading, out bool extendStart, out bool extendEnd);

            BuildShadingColorsAndStops(shading, page, gs.RenderingIntent, out var colors, out var positions);
            if (colors == null || colors.Length == 0)
            {
                return;
            }

            // Determine tile mode based on extend flags
            SKShaderTileMode tileMode = SKShaderTileMode.Clamp;
            if (extendStart && extendEnd)
            {
                // Both ends extend - use clamp (SkiaSharp default behavior)
                tileMode = SKShaderTileMode.Clamp;
            }
            else if (!extendStart && !extendEnd)
            {
                // No extension - gradient should be clamped to defined region
                tileMode = SKShaderTileMode.Decal; // Or handle clipping manually
            }

            using (var shader = SKShader.CreateLinearGradient(new SKPoint(x0, y0), new SKPoint(x1, y1), colors, positions, tileMode))
            using (var paint = PdfPaintFactory.CreateShadingPaint(gs, shader, page))
            {
                canvas.DrawPaint(paint);
            }
        }

        private static void DrawRadialShading(PdfDictionary shading, PdfGraphicsState gs, SKCanvas canvas, PdfPage page)
        {
            var coords = shading.GetArray(PdfTokens.CoordsKey);
            if (coords == null || coords.Count < 6)
            {
                return;
            }

            float x0 = coords[0].AsFloat();
            float y0 = coords[1].AsFloat();
            float r0 = Math.Max(0f, coords[2].AsFloat());
            float x1 = coords[3].AsFloat();
            float y1 = coords[4].AsFloat();
            float r1 = Math.Max(0f, coords[5].AsFloat());
            GetExtend(shading, out bool extendStart, out bool extendEnd);

            BuildShadingColorsAndStops(shading, page, gs.RenderingIntent, out var colors, out var positions);
            if (colors == null || colors.Length == 0)
            {
                return;
            }

            if (r0 > r1)
            {
                (x0, x1) = (x1, x0);
                (y0, y1) = (y1, y0);
                (r0, r1) = (r1, r0);
                Array.Reverse(colors);
                Array.Reverse(positions);
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = 1f - positions[i];
                }
            }

            using (var shader = SKShader.CreateTwoPointConicalGradient(new SKPoint(x0, y0), r0, new SKPoint(x1, y1), r1, colors, positions, SKShaderTileMode.Clamp))
            using (var paint = PdfPaintFactory.CreateShadingPaint(gs, shader, page))
            {
                if (!extendEnd)
                {
                    var clip = new SKPath();
                    clip.AddCircle(x1, y1, r1);
                    canvas.Save();
                    canvas.ClipPath(clip, antialias: true);
                    canvas.DrawPaint(paint);
                    canvas.Restore();
                }
                else
                {
                    canvas.DrawPaint(paint);
                }
            }
        }

        private static void GetExtend(PdfDictionary shading, out bool extendStart, out bool extendEnd)
        {
            extendStart = false;
            extendEnd = false;
            var extendArr = shading.GetArray(PdfTokens.ExtendKey);
            if (extendArr != null && extendArr.Count >= 2)
            {
                extendStart = extendArr[0].AsBool();
                extendEnd = extendArr[1].AsBool();
            }
        }

        private static void BuildShadingColorsAndStops(PdfDictionary shading, PdfPage page, PdfRenderingIntent intent, out SKColor[] colors, out float[] positions)
        {
            var csVal = shading.GetValue(PdfTokens.ColorSpaceKey);
            var converter = PdfColorSpaces.ResolveByValue(csVal, page) ?? DeviceRgbConverter.Instance;

            var domain = shading.GetArray(PdfTokens.DomainKey);
            float d0 = 0f;
            float d1 = 1f;
            if (domain != null && domain.Count >= 2)
            {
                d0 = domain[0].AsFloat();
                d1 = domain[1].AsFloat();
                if (Math.Abs(d1 - d0) < 1e-9f)
                {
                    d1 = d0 + 1f;
                }
            }

            var funcVal = shading.GetValue(PdfTokens.FunctionKey);
            if (funcVal != null)
            {
                const int SampleCount = 64; // basic sampling density
                colors = new SKColor[SampleCount];
                positions = new float[SampleCount];
                for (int i = 0; i < SampleCount; i++)
                {
                    float t = i / (float)(SampleCount - 1);
                    float x = d0 + t * (d1 - d0);
                    var comps = PdfFunctions.EvaluateColorFunctions(shading, x);
                    colors[i] = converter.ToSrgb(comps, intent);
                    positions[i] = t;
                }
                return;
            }

            var c0Arr = shading.GetArray(PdfTokens.C0Key);
            var c1Arr = shading.GetArray(PdfTokens.C1Key);
            if (c0Arr != null && c1Arr != null)
            {
                var c0 = ToFloatArray(c0Arr);
                var c1 = ToFloatArray(c1Arr);
                colors = new[] { converter.ToSrgb(c0, intent), converter.ToSrgb(c1, intent) };
                positions = new[] { 0f, 1f };
                return;
            }

            colors = new[] { SKColors.Black, SKColors.White };
            positions = new[] { 0f, 1f };
        }

        private static float[] ToFloatArray(List<IPdfValue> arr)
        {
            var vals = new float[arr.Count];
            for (int i = 0; i < arr.Count; i++) vals[i] = arr[i].AsFloat();
            return vals;
        }
    }
}
