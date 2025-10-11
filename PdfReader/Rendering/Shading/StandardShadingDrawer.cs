using System;
using SkiaSharp;
using PdfReader.Models;
using PdfReader.Rendering.Color;
using Microsoft.Extensions.Logging;
using PdfReader.Rendering.Advanced;

namespace PdfReader.Rendering.Shading
{
    /// <summary>
    /// Draws axial (type 2) and radial (type 3) shadings using parsed <see cref="PdfShading"/> model.
    /// Applies soft mask scope once per shading draw.
    /// </summary>
    public class StandardShadingDrawer : IShadingDrawer
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<StandardShadingDrawer> _logger;

        public StandardShadingDrawer(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<StandardShadingDrawer>();
        }

        /// <summary>
        /// Draw a shading fill described by a parsed shading model, applying soft mask if present.
        /// </summary>
        public void DrawShading(SKCanvas canvas, PdfShading shading, PdfGraphicsState state, PdfPage page)
        {
            if (canvas == null)
            {
                return;
            }
            if (shading == null)
            {
                return;
            }

            using (var softMaskScope = new SoftMaskDrawingScope(canvas, state, page))
            {
                softMaskScope.BeginDrawContent();
                DrawShadingCore(canvas, shading, state, page);
                softMaskScope.EndDrawContent();
            }
        }

        /// <summary>
        /// Core shading dispatch logic without soft mask handling.
        /// </summary>
        private void DrawShadingCore(SKCanvas canvas, PdfShading shading, PdfGraphicsState state, PdfPage page)
        {
            switch (shading.ShadingType)
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
                    _logger.LogWarning("Shading type " + shading.ShadingType + " not implemented");
                    break;
                }
            }
        }

        private static void DrawAxialShading(PdfShading shading, PdfGraphicsState graphicsState, SKCanvas canvas, PdfPage page)
        {
            if (shading.Coords == null || shading.Coords.Length < 4)
            {
                return;
            }

            float x0 = shading.Coords[0];
            float y0 = shading.Coords[1];
            float x1 = shading.Coords[2];
            float y1 = shading.Coords[3];

            BuildShadingColorsAndStops(shading, page, graphicsState.RenderingIntent, out var colors, out var positions);
            if (colors == null || colors.Length == 0)
            {
                return;
            }

            SKShaderTileMode tileMode = SKShaderTileMode.Clamp;
            if (!shading.ExtendStart && !shading.ExtendEnd)
            {
                tileMode = SKShaderTileMode.Decal;
            }

            using (var shader = SKShader.CreateLinearGradient(new SKPoint(x0, y0), new SKPoint(x1, y1), colors, positions, tileMode))
            using (var paint = PdfPaintFactory.CreateShadingPaint(graphicsState, shader, page))
            {
                canvas.DrawPaint(paint);
            }
        }

        private static void DrawRadialShading(PdfShading shading, PdfGraphicsState graphicsState, SKCanvas canvas, PdfPage page)
        {
            if (shading.Coords == null || shading.Coords.Length < 6)
            {
                return;
            }

            float x0 = shading.Coords[0];
            float y0 = shading.Coords[1];
            float r0 = Math.Max(0f, shading.Coords[2]);
            float x1 = shading.Coords[3];
            float y1 = shading.Coords[4];
            float r1 = Math.Max(0f, shading.Coords[5]);

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
                if (!shading.ExtendEnd)
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

        private static void BuildShadingColorsAndStops(PdfShading shading, PdfPage page, PdfRenderingIntent intent, out SKColor[] colors, out float[] positions)
        {
            var converter = PdfColorSpaces.ResolveByValue(shading.ColorSpaceValue, page) ?? DeviceRgbConverter.Instance;

            float d0 = 0f;
            float d1 = 1f;
            if (shading.Domain != null && shading.Domain.Length >= 2)
            {
                d0 = shading.Domain[0];
                d1 = shading.Domain[1];
                if (Math.Abs(d1 - d0) < 1e-9f)
                {
                    d1 = d0 + 1f;
                }
            }

            if (shading.HasFunction)
            {
                const int SampleCount = 64;
                colors = new SKColor[SampleCount];
                positions = new float[SampleCount];
                for (int index = 0; index < SampleCount; index++)
                {
                    float t = index / (float)(SampleCount - 1);
                    float x = d0 + t * (d1 - d0);
                    var comps = PdfFunctions.EvaluateColorFunctions(shading.RawDictionary, x);
                    colors[index] = converter.ToSrgb(comps, intent);
                    positions[index] = t;
                }
                return;
            }

            if (shading.C0 != null && shading.C1 != null)
            {
                colors = new[] { converter.ToSrgb(shading.C0, intent), converter.ToSrgb(shading.C1, intent) };
                positions = new[] { 0f, 1f };
                return;
            }

            colors = new[] { SKColors.Black, SKColors.White };
            positions = new[] { 0f, 1f };
        }
    }
}
