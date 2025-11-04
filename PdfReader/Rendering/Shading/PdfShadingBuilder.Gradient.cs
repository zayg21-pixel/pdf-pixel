using PdfReader.Models;
using PdfReader.Rendering.Functions;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Shading
{
    internal static partial class PdfShadingBuilder
    {
        private static SKShader BuildAxial(PdfShading shading)
        {
            if (shading.Coords?.Length != 4)
            {
                return null;
            }

            BuildShadingColorsAndStops(shading, out var colors, out var positions);
            if (colors == null || colors.Length == 0)
            {
                return null;
            }

            SKPoint start = new SKPoint(shading.Coords[0], shading.Coords[1]);
            SKPoint end = new SKPoint(shading.Coords[2], shading.Coords[3]);

            return SKShader.CreateLinearGradient(
                start,
                end,
                colors,
                positions,
                SKShaderTileMode.Clamp);
        }

        private static SKShader BuildRadial(PdfShading shading)
        {
            if (shading.Coords?.Length != 6)
            {
                return null;
            }

            BuildShadingColorsAndStops(shading, out var colors, out var positions);
            if (colors == null || colors.Length == 0)
            {
                return null;
            }

            SKPoint center0 = new SKPoint(shading.Coords[0], shading.Coords[1]);
            SKPoint center1 = new SKPoint(shading.Coords[3], shading.Coords[4]);
            float r0 = shading.Coords[2];
            float r1 = shading.Coords[5];

            // Handle reversed radii
            if (r0 > r1)
            {
                SKPoint tempCenter = center0;
                center0 = center1;
                center1 = tempCenter;
                float tempRadius = r0;
                r0 = r1;
                r1 = tempRadius;
                Array.Reverse(colors);
                Array.Reverse(positions);
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = 1f - positions[i];
                }
            }

            return SKShader.CreateTwoPointConicalGradient(
                center0, r0,
                center1, r1,
                colors,
                positions,
                SKShaderTileMode.Clamp);
        }

        /// <summary>
        /// Builds the color and position arrays for a shading, used in gradient creation.
        /// </summary>
        /// <param name="shading">Parsed shading model.</param>
        /// <param name="colors">Output array of SKColor stops.</param>
        /// <param name="positions">Output array of gradient positions.</param>
        private static void BuildShadingColorsAndStops(
            PdfShading shading,
            out SKColor[] colors,
            out float[] positions)
        {
            var converter = shading.ColorSpaceConverter;

            float domainStart = 0f;
            float domainEnd = 1f;
            if (shading.Domain != null && shading.Domain.Length >= 2)
            {
                domainStart = shading.Domain[0];
                domainEnd = shading.Domain[1];
                if (Math.Abs(domainEnd - domainStart) < 1e-9f)
                {
                    domainEnd = domainStart + 1f;
                }
            }

            if (shading.Functions.Count > 0)
            {
                const int SampleCount = 64;
                colors = new SKColor[SampleCount];
                positions = new float[SampleCount];
                for (int sampleIndex = 0; sampleIndex < SampleCount; sampleIndex++)
                {
                    float t = sampleIndex / (float)(SampleCount - 1);
                    float x = domainStart + t * (domainEnd - domainStart);
                    var comps = PdfFunctions.EvaluateColorFunctions(shading.Functions, x);
                    colors[sampleIndex] = converter.ToSrgb(comps, shading.RenderingIntent);
                    positions[sampleIndex] = t;
                }
            }
            else if (shading.C0 != null && shading.C1 != null)
            {
                colors =
                [
                    converter.ToSrgb(shading.C0, shading.RenderingIntent),
                    converter.ToSrgb(shading.C1, shading.RenderingIntent)
                ];
                positions = [0f, 1f];
            }
            else
            {
                colors = [SKColors.Black, SKColors.White];
                positions = [0f, 1f];
            }

            if (!shading.ExtendEnd || !shading.ExtendStart)
            {

                var listPositions = new List<float>(positions);
                var listColors = new List<SKColor>(colors);

                if (!shading.ExtendStart)
                {
                    float start = positions[0];
                    listPositions.Insert(0, start);
                    listColors.Insert(0, SKColors.Transparent);
                }

                if (!shading.ExtendEnd)
                {
                    float end = positions[positions.Length - 1];
                    listPositions.Add(end);
                    listColors.Add(SKColors.Transparent);
                }

                positions = listPositions.ToArray();
                colors = listColors.ToArray();
            }
        }
    }
}
