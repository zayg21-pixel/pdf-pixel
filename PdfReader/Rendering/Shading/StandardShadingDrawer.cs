using System;
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

            int type = shading.GetIntegerOrDefault(PdfTokens.ShadingTypeKey);
            switch (type)
            {
                case 2:
                {
                    DrawAxialShading(shading, state, canvas, page);
                    break;
                }
                case 3:
                {
                    DrawRadialShading(shading, state, canvas, page);
                    break;
                }
                default:
                {
                    Console.WriteLine("Shading type " + type + " not implemented");
                    break;
                }
            }
        }

        private static void DrawAxialShading(PdfDictionary shading, PdfGraphicsState graphicsState, SKCanvas canvas, PdfPage page)
        {
            var coordsArray = shading.GetArray(PdfTokens.CoordsKey);
            if (coordsArray == null || coordsArray.Count < 4)
            {
                return;
            }

            float x0 = coordsArray.GetFloat(0);
            float y0 = coordsArray.GetFloat(1);
            float x1 = coordsArray.GetFloat(2);
            float y1 = coordsArray.GetFloat(3);

            GetExtend(shading, out bool extendStart, out bool extendEnd);

            BuildShadingColorsAndStops(shading, page, graphicsState.RenderingIntent, out var colors, out var positions);
            if (colors == null || colors.Length == 0)
            {
                return;
            }

            SKShaderTileMode tileMode = SKShaderTileMode.Clamp;
            if (!extendStart && !extendEnd)
            {
                tileMode = SKShaderTileMode.Decal;
            }

            using (var shader = SKShader.CreateLinearGradient(new SKPoint(x0, y0), new SKPoint(x1, y1), colors, positions, tileMode))
            using (var paint = PdfPaintFactory.CreateShadingPaint(graphicsState, shader, page))
            {
                canvas.DrawPaint(paint);
            }
        }

        private static void DrawRadialShading(PdfDictionary shading, PdfGraphicsState graphicsState, SKCanvas canvas, PdfPage page)
        {
            var coordsArray = shading.GetArray(PdfTokens.CoordsKey);
            if (coordsArray == null || coordsArray.Count < 6)
            {
                return;
            }

            float x0 = coordsArray.GetFloat(0);
            float y0 = coordsArray.GetFloat(1);
            float r0 = Math.Max(0f, coordsArray.GetFloat(2));
            float x1 = coordsArray.GetFloat(3);
            float y1 = coordsArray.GetFloat(4);
            float r1 = Math.Max(0f, coordsArray.GetFloat(5));

            GetExtend(shading, out bool extendStart, out bool extendEnd);

            BuildShadingColorsAndStops(shading, page, graphicsState.RenderingIntent, out var colors, out var positions);
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
                for (int index = 0; index < positions.Length; index++)
                {
                    positions[index] = 1f - positions[index];
                }
            }

            using (var shader = SKShader.CreateTwoPointConicalGradient(new SKPoint(x0, y0), r0, new SKPoint(x1, y1), r1, colors, positions, SKShaderTileMode.Clamp))
            using (var paint = PdfPaintFactory.CreateShadingPaint(graphicsState, shader, page))
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
            var extendArray = shading.GetArray(PdfTokens.ExtendKey);
            if (extendArray != null && extendArray.Count >= 2)
            {
                extendStart = extendArray.GetBool(0);
                extendEnd = extendArray.GetBool(1);
            }
        }

        private static void BuildShadingColorsAndStops(PdfDictionary shading, PdfPage page, PdfRenderingIntent intent, out SKColor[] colors, out float[] positions)
        {
            var csVal = shading.GetValue(PdfTokens.ColorSpaceKey);
            var converter = PdfColorSpaces.ResolveByValue(csVal, page) ?? DeviceRgbConverter.Instance;

            float d0 = 0f;
            float d1 = 1f;
            var domainArray = shading.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
            if (domainArray != null && domainArray.Length >= 2)
            {
                d0 = domainArray[0];
                d1 = domainArray[1];
                if (Math.Abs(d1 - d0) < 1e-9f)
                {
                    d1 = d0 + 1f;
                }
            }

            var funcVal = shading.GetValue(PdfTokens.FunctionKey);
            if (funcVal != null)
            {
                const int SampleCount = 64;
                colors = new SKColor[SampleCount];
                positions = new float[SampleCount];
                for (int index = 0; index < SampleCount; index++)
                {
                    float t = index / (float)(SampleCount - 1);
                    float x = d0 + t * (d1 - d0);
                    var comps = PdfFunctions.EvaluateColorFunctions(shading, x);
                    colors[index] = converter.ToSrgb(comps, intent);
                    positions[index] = t;
                }
                return;
            }

            var c0 = shading.GetArray(PdfTokens.C0Key)?.GetFloatArray();
            var c1 = shading.GetArray(PdfTokens.C1Key)?.GetFloatArray();
            if (c0 != null && c1 != null)
            {
                colors = new[] { converter.ToSrgb(c0, intent), converter.ToSrgb(c1, intent) };
                positions = new[] { 0f, 1f };
                return;
            }

            colors = new[] { SKColors.Black, SKColors.White };
            positions = new[] { 0f, 1f };
        }
    }
}
