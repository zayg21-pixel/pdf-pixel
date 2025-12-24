using PdfReader.Color.Paint;
using PdfReader.Functions;
using PdfReader.Shading.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfReader.Shading;

internal static partial class PdfShadingBuilder
{
    private static SKPicture BuildAxial(PdfShading shading, SKRect bounds)
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

        using var shader = SKShader.CreateLinearGradient(
            start,
            end,
            colors,
            positions,
            SKShaderTileMode.Clamp);

        using var pictureRecorder = new SKPictureRecorder();
        using var canvas = pictureRecorder.BeginRecording(bounds);

        using var basePaint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias);
        basePaint.Shader = shader;

        canvas.DrawPaint(basePaint);

        return pictureRecorder.EndRecording();
    }

    private static SKPicture BuildRadial(PdfShading shading, SKRect bounds)
    {
        if (shading.Coords?.Length != 6)
        {
            return null;
        }

        SKPoint center0 = new SKPoint(shading.Coords[0], shading.Coords[1]);
        SKPoint center1 = new SKPoint(shading.Coords[3], shading.Coords[4]);
        float r0 = shading.Coords[2];
        float r1 = shading.Coords[5];

        BuildShadingColorsAndStops(shading, out var colors, out var positions);

        if (colors == null || colors.Length == 0)
        {
            return null;
        }

        using var pictureRecorder = new SKPictureRecorder();
        using var canvas = pictureRecorder.BeginRecording(bounds);

        // first pass, draw inner surface part
        SKPoint reversedCenter0 = center1;
        SKPoint reversedCenter1 = center0;
        float reversedR0 = r1;
        float reversedR1 = r0;

        var reversedColors = colors.AsEnumerable().Reverse().ToArray();
        var reversedPositions = positions.AsEnumerable().Reverse().ToArray();

        for (int i = 0; i < reversedPositions.Length; i++)
        {
            reversedPositions[i] = 1 - reversedPositions[i];
        }

        using var reversedPaint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias);

        using var reversedShader = SKShader.CreateTwoPointConicalGradient(
            reversedCenter0, reversedR0,
            reversedCenter1, reversedR1,
            reversedColors,
            reversedPositions,
            SKShaderTileMode.Clamp);

        reversedPaint.Shader = reversedShader;

        // second pass, draw outer surface part
        canvas.DrawPaint(reversedPaint);

        using var basePaint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias);

        using var shader = SKShader.CreateTwoPointConicalGradient(
            center0, r0,
            center1, r1,
            colors,
            positions,
            SKShaderTileMode.Clamp);

        basePaint.Shader = shader;

        canvas.DrawPaint(basePaint);

        return pictureRecorder.EndRecording();
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
            const int SampleCount = 256;
            // TODO: well, this does not really work, we need a better sampling strategy, basically, same issue as with shading type 1
            // when where's sampled function involved, we need a pixel-accurate sampling based on the actual gradient length in device space
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
