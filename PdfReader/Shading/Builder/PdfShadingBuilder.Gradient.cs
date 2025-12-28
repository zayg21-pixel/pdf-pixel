using PdfReader.Color.Paint;
using PdfReader.Functions;
using PdfReader.Rendering.State;
using PdfReader.Shading.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfReader.Shading;

internal static partial class PdfShadingBuilder
{
    private static SKPicture BuildAxial(PdfShading shading, PdfGraphicsState state, SKRect bounds)
    {
        if (shading.Coords?.Length != 4)
        {
            return null;
        }

        BuildShadingColorsAndStops(shading, state, out var colors, out var positions);
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

        using var basePaint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias && !state.RenderingParameters.PreviewMode);
        basePaint.Shader = shader;

        canvas.DrawPaint(basePaint);

        return pictureRecorder.EndRecording();
    }

    private static SKPicture BuildRadial(PdfShading shading, PdfGraphicsState state, SKRect bounds)
    {
        if (shading.Coords?.Length != 6)
        {
            return null;
        }

        SKPoint center0 = new SKPoint(shading.Coords[0], shading.Coords[1]);
        SKPoint center1 = new SKPoint(shading.Coords[3], shading.Coords[4]);
        float r0 = shading.Coords[2];
        float r1 = shading.Coords[5];

        BuildShadingColorsAndStops(shading, state, out var colors, out var positions);

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

        using var reversedPaint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias && !state.RenderingParameters.PreviewMode);

        using var reversedShader = SKShader.CreateTwoPointConicalGradient(
            reversedCenter0, reversedR0,
            reversedCenter1, reversedR1,
            reversedColors,
            reversedPositions,
            SKShaderTileMode.Clamp);

        reversedPaint.Shader = reversedShader;

        // second pass, draw outer surface part
        canvas.DrawPaint(reversedPaint);

        using var basePaint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias && !state.RenderingParameters.PreviewMode);

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
        PdfGraphicsState state,
        out SKColor[] colors,
        out float[] positions)
    {
        var converter = state.Page.Cache.ColorSpace.ResolveByObject(shading.ColorSpaceConverter);

        float domainStart = 0f;
        float domainEnd = 1f;
        if (shading.Domain != null && shading.Domain.Length >= 2)
        {
            domainStart = shading.Domain[0];
            domainEnd = shading.Domain[1];
        }

        if (shading.Functions.Count > 0)
        {
            PdfFunction primaryFunction = shading.Functions[0];
            float[] sampleXs = primaryFunction.GetSamplingPoints(0, domainStart, domainEnd);

            positions = new float[sampleXs.Length];
            colors = new SKColor[sampleXs.Length];

            float domainLength = domainEnd - domainStart;
            if (domainLength == 0f)
            {
                domainLength = 1f;
            }

            for (int i = 0; i < sampleXs.Length; i++)
            {
                float x = sampleXs[i];
                float t = (x - domainStart) / domainLength;
                var comps = PdfFunctions.EvaluateColorFunctions(shading.Functions, x);
                colors[i] = converter.ToSrgb(comps, state.RenderingIntent);
                positions[i] = t;
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
