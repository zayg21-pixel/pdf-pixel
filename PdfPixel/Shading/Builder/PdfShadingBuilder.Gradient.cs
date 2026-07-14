using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Shading.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.Shading;

internal partial class PdfShadingBuilder
{
    /// <summary>
    /// Builds an axial (Type 2) gradient paint with a linear gradient shader.
    /// Returns null if the shading coordinates are invalid.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="colors">Pre-computed color stops.</param>
    /// <param name="positions">Pre-computed gradient positions.</param>
    /// <returns>Paint with gradient shader, or null on failure.</returns>
    public SKPaint? BuildAxialPaint(PdfShading shading, SKColor[] colors, float[] positions)
    {
        if (shading.Coords?.Length != 4)
        {
            _logger.LogWarning("Axial shading requires exactly 4 coordinates");
            return null;
        }

        SKPoint start = new(shading.Coords[0], shading.Coords[1]);
        SKPoint end = new(shading.Coords[2], shading.Coords[3]);

        using SKShader shader = SKShader.CreateLinearGradient(
            start,
            end,
            colors,
            positions,
            SKShaderTileMode.Clamp);

        SKPaint paint = PdfPaintFactory.CreateShaderPaint();
        paint.Shader = shader;

        return paint;
    }

    /// <summary>
    /// Builds radial (Type 3) gradient paints using two-point conical gradients.
    /// Returns an inner (reversed) and outer paint for the two rendering passes.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="colors">Pre-computed color stops.</param>
    /// <param name="positions">Pre-computed gradient positions.</param>
    /// <returns>A <see cref="RadialShadingPaints"/> with inner and outer paints, or <see langword="null"/> if coordinates are invalid.</returns>
    public RadialShadingPaints? BuildRadialPaints(PdfShading shading, SKColor[] colors, float[] positions)
    {
        if (shading.Coords?.Length != 6)
        {
            _logger.LogWarning("Radial shading requires exactly 6 coordinates");
            return null;
        }

        SKPoint center0 = new(shading.Coords[0], shading.Coords[1]);
        SKPoint center1 = new(shading.Coords[3], shading.Coords[4]);
        float r0 = shading.Coords[2];
        float r1 = shading.Coords[5];

        // Inner surface (reversed direction)
        SKPoint reversedCenter0 = center1;
        SKPoint reversedCenter1 = center0;
        float reversedR0 = r1;
        float reversedR1 = r0;

        SKColor[] reversedColors = colors.AsEnumerable().Reverse().ToArray();
        float[] reversedPositions = positions.AsEnumerable().Reverse().ToArray();

        for (int i = 0; i < reversedPositions.Length; i++)
        {
            reversedPositions[i] = 1 - reversedPositions[i];
        }

        using SKShader reversedShader = SKShader.CreateTwoPointConicalGradient(
            reversedCenter0,
            reversedR0,
            reversedCenter1,
            reversedR1,
            reversedColors,
            reversedPositions,
            SKShaderTileMode.Clamp);

        SKPaint innerPaint = PdfPaintFactory.CreateShaderPaint();
        innerPaint.Shader = reversedShader;

        // Outer surface
        using SKShader shader = SKShader.CreateTwoPointConicalGradient(
            center0,
            r0,
            center1,
            r1,
            colors,
            positions,
            SKShaderTileMode.Clamp);

        SKPaint outerPaint = PdfPaintFactory.CreateShaderPaint();
        outerPaint.Shader = shader;

        return new RadialShadingPaints(innerPaint, outerPaint);
    }

    /// <summary>
    /// Builds the color and position arrays for a shading by evaluating its functions
    /// across the domain range.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="sampler">RGBA sampler for color conversion.</param>
    /// <param name="defaultFunctionSamples">Number of function samples to use.</param>
    /// <param name="colors">Output array of SKColor stops.</param>
    /// <param name="positions">Output array of gradient positions.</param>
    public void BuildShadingColorsAndStops(
        PdfShading shading,
        ColorTransformSampler sampler,
        int defaultFunctionSamples,
        out SKColor[] colors,
        out float[] positions)
    {
        float domainStart = 0f;
        float domainEnd = 1f;
        if (shading.Domain?.Length >= 2)
        {
            domainStart = shading.Domain[0];
            domainEnd = shading.Domain[1];
        }

        if (shading.Functions.Count > 0)
        {
            PdfFunction primaryFunction = shading.Functions[0];

            float[] sampleXs = primaryFunction.GetSamplingPoints(0, domainStart, domainEnd, defaultFunctionSamples);

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
                ReadOnlySpan<float> comps = PdfFunctions.EvaluateColorFunctions(shading.Functions, x);
                colors[i] = sampler.Sample(comps).From01ToSkiaColor();
                positions[i] = t;
            }
        }
        else
        {
            colors = new SKColor[] { SKColors.Black, SKColors.White };
            positions = new float[] { 0f, 1f };
        }

        if (!shading.ExtendEnd || !shading.ExtendStart)
        {
            List<float> listPositions = new(positions);
            List<SKColor> listColors = new(colors);

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
