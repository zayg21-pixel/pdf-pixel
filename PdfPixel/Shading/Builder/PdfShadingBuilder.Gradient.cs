using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Functions;
using PdfPixel.Geometry;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Shading.Builder;

internal partial class PdfShadingBuilder
{
    /// <summary>
    /// Builds an axial (Type 2) gradient model.
    /// Returns null if the shading coordinates are invalid.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="colorStops">Pre-computed color and position stops.</param>
    /// <returns>The gradient model, or null on failure.</returns>
    public PdfAxialGradient? BuildAxialGradient(PdfShading shading, PdfShadingColorStops colorStops)
    {
        if (shading.Coordinates?.Length != 4)
        {
            _logger.LogWarning("Axial shading requires exactly 4 coordinates");
            return null;
        }

        PdfPoint start = new(shading.Coordinates[0], shading.Coordinates[1]);
        PdfPoint end = new(shading.Coordinates[2], shading.Coordinates[3]);

        return new PdfAxialGradient(start, end, colorStops.Colors, colorStops.Positions, shading.ExtendStart, shading.ExtendEnd);
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
        if (shading.Coordinates?.Length != 6)
        {
            _logger.LogWarning("Radial shading requires exactly 6 coordinates");
            return null;
        }

        PdfPoint center0 = new(shading.Coordinates[0], shading.Coordinates[1]);
        PdfPoint center1 = new(shading.Coordinates[3], shading.Coordinates[4]);
        float r0 = shading.Coordinates[2];
        float r1 = shading.Coordinates[5];

        return new PdfRadialGradient(center0, r0, center1, r1, colorStops.Colors, colorStops.Positions, shading.ExtendStart, shading.ExtendEnd);
    }

    /// <summary>
    /// Builds the color and position arrays for a shading by evaluating its functions
    /// across the domain range.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="palette">Palette the function values index into, or null when the shading's color space is not Indexed.</param>
    /// <param name="defaultFunctionSamples">Number of function samples to use.</param>
    /// <returns>The computed color and position stops, or null when the shading has no function.</returns>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public PdfShadingColorStops? BuildShadingColorsAndStops(PdfShading shading, PdfPalette? palette, int defaultFunctionSamples)
    {
        if (shading.Functions.Length == 0)
        {
            _logger.LogWarning("Axial and radial shadings require a function");
            return null;
        }

        float domainStart = 0f;
        float domainEnd = 1f;
        if (shading.Domain?.Length >= 1)
        {
            domainStart = shading.Domain[0].Min;
            domainEnd = shading.Domain[0].Max;
        }

        PdfFunction primaryFunction = shading.Functions[0];

        float[] sampleXs = primaryFunction.GetSamplingPoints(0, domainStart, domainEnd, defaultFunctionSamples);

        var positions = new float[sampleXs.Length];
        var colors = new PdfColor[sampleXs.Length];

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
            if (palette != null)
            {
                colors[i] = palette.GetColor((comps.Length > 0) ? comps[0] : 0f);
            }
            else
            {
                colors[i] = new PdfColor(comps.ToArray());
            }

            positions[i] = t;
        }

        return new PdfShadingColorStops(colors, positions);
    }
}
