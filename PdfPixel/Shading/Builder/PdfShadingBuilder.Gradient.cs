using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Geometry;
using PdfPixel.Shading.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Shading;

internal partial class PdfShadingBuilder
{
    /// <summary>
    /// Builds an axial (Type 2) gradient model.
    /// Returns null if the shading coordinates are invalid.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="colorStops">Pre-computed color and position stops.</param>
    /// <returns>The gradient model, or null on failure.</returns>
    public PdfLinearGradient? BuildLinearGradient(PdfShading shading, PdfShadingColorStops colorStops)
    {
        if (shading.Coords?.Length != 4)
        {
            _logger.LogWarning("Axial shading requires exactly 4 coordinates");
            return null;
        }

        PdfPoint start = new(shading.Coords[0], shading.Coords[1]);
        PdfPoint end = new(shading.Coords[2], shading.Coords[3]);

        return new PdfLinearGradient(start, end, colorStops.Colors, colorStops.Positions);
    }

    /// <summary>
    /// Builds a radial (Type 3) gradient model.
    /// Returns null if the shading coordinates are invalid.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="colorStops">Pre-computed color and position stops.</param>
    /// <returns>The gradient model, or null on failure.</returns>
    public PdfRadialGradient? BuildRadialGradient(PdfShading shading, PdfShadingColorStops colorStops)
    {
        if (shading.Coords?.Length != 6)
        {
            _logger.LogWarning("Radial shading requires exactly 6 coordinates");
            return null;
        }

        PdfPoint center0 = new(shading.Coords[0], shading.Coords[1]);
        PdfPoint center1 = new(shading.Coords[3], shading.Coords[4]);
        float r0 = shading.Coords[2];
        float r1 = shading.Coords[5];

        return new PdfRadialGradient(center0, r0, center1, r1, colorStops.Colors, colorStops.Positions);
    }

    /// <summary>
    /// Builds the color and position arrays for a shading by evaluating its functions
    /// across the domain range.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="sampler">RGBA sampler for color conversion.</param>
    /// <param name="defaultFunctionSamples">Number of function samples to use.</param>
    /// <returns>The computed color and position stops.</returns>
    public PdfShadingColorStops BuildShadingColorsAndStops(
        PdfShading shading,
        ColorTransformSampler sampler,
        int defaultFunctionSamples)
    {
        float domainStart = 0f;
        float domainEnd = 1f;
        if (shading.Domain?.Length >= 1)
        {
            domainStart = shading.Domain[0].Min;
            domainEnd = shading.Domain[0].Max;
        }

        PdfColor[] colors;
        float[] positions;

        if (shading.Functions.Count > 0)
        {
            PdfFunction primaryFunction = shading.Functions[0];

            float[] sampleXs = primaryFunction.GetSamplingPoints(0, domainStart, domainEnd, defaultFunctionSamples);

            positions = new float[sampleXs.Length];
            colors = new PdfColor[sampleXs.Length];

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
                colors[i] = sampler.Sample(comps).ToPdfColor();
                positions[i] = t;
            }
        }
        else
        {
            colors = new PdfColor[] { PdfColors.Black, PdfColors.White };
            positions = new float[] { 0f, 1f };
        }

        if (!shading.ExtendEnd || !shading.ExtendStart)
        {
            List<float> listPositions = new(positions);
            List<PdfColor> listColors = new(colors);

            if (!shading.ExtendStart)
            {
                float start = positions[0];
                listPositions.Insert(0, start);
                listColors.Insert(0, PdfColors.Transparent);
            }

            if (!shading.ExtendEnd)
            {
                float end = positions[positions.Length - 1];
                listPositions.Add(end);
                listColors.Add(PdfColors.Transparent);
            }

            positions = listPositions.ToArray();
            colors = listColors.ToArray();
        }

        return new PdfShadingColorStops(colors, positions);
    }
}
