using Microsoft.Extensions.Logging;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Commands;
using PdfPixel.Functions;
using PdfPixel.Rendering.Operators;
using PdfPixel.Shading.Model;
using PdfPixel.Text;
using SkiaSharp;
using System;

namespace PdfPixel.Shading;

internal partial class PdfShadingBuilder
{
    /// <summary>
    /// Builds a function-based (Type 1) shading bitmap and the matrix that maps
    /// bitmap pixel space into the shading coordinate system.
    /// Returns <see langword="null"/> if the shading is invalid or degenerate.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="sampler">RGBA sampler for color conversion.</param>
    /// <param name="defaultFunctionSamples">Number of function samples to use.</param>
    /// <param name="observer">Execution observer for progress and cancellation.</param>
    /// <returns>A <see cref="FunctionShadingResult"/> containing the bitmap and matrix, or <see langword="null"/> on failure.</returns>
    public FunctionShadingResult? BuildFunctionBasedBitmap(
        PdfShading shading,
        ColorTransformSampler sampler,
        int defaultFunctionSamples,
        IPdfExecutionObserver observer)
    {
        if (shading.Functions == null || shading.Functions.Count == 0 || shading.ColorSpaceObject == null)
        {
            _logger.LogWarning("Function-based shading has no functions or color space converter");
            return null;
        }

        PdfFunction function = shading.Functions[0];

        float domainX0 = 0f;
        float domainX1 = 1f;
        float domainY0 = 0f;
        float domainY1 = 1f;
        if (shading.Domain?.Length >= 2)
        {
            domainX0 = shading.Domain[0].Min;
            domainX1 = shading.Domain[0].Max;
            domainY0 = shading.Domain[1].Min;
            domainY1 = shading.Domain[1].Max;
        }

        float domainWidth = Math.Abs(domainX1 - domainX0);
        float domainHeight = Math.Abs(domainY1 - domainY0);
        if (domainWidth < 1e-6f || domainHeight < 1e-6f)
        {
            _logger.LogWarning("Function-based shading has degenerate domain dimensions");
            return null;
        }

        float[] xSamples = function.GetSamplingPoints(0, domainX0, domainX1, defaultFunctionSamples);
        float[] ySamples = function.GetSamplingPoints(1, domainY0, domainY1, defaultFunctionSamples);

        int bitmapWidth = Math.Max(1, xSamples.Length);
        int bitmapHeight = Math.Max(1, ySamples.Length);

        SKBitmap bitmap = new(bitmapWidth, bitmapHeight);
        var pixelColors = new SKColor[bitmapWidth * bitmapHeight];
        for (int yIndex = 0; yIndex < bitmapHeight; yIndex++)
        {
            float domainY = ySamples[yIndex];
            for (int xIndex = 0; xIndex < bitmapWidth; xIndex++)
            {
                float domainX = xSamples[xIndex];
                ReadOnlySpan<float> comps = function.Evaluate([domainX, domainY]);
                SKColor color = sampler.Sample(comps).From01ToSkiaColor();
                pixelColors[(yIndex * bitmapWidth) + xIndex] = color;
            }

            observer?.Notify();
        }

        bitmap.Pixels = pixelColors;

        // Compute matrix to map bitmap pixel space to domain rectangle
        float scaleX = domainWidth / bitmapWidth;
        float scaleY = domainHeight / bitmapHeight;
        float translateX = domainX0;
        float translateY = domainY0;
        SKMatrix pixelToDomain = SKMatrix.CreateScale(scaleX, scaleY);
        pixelToDomain = SKMatrix.Concat(SKMatrix.CreateTranslation(translateX, translateY), pixelToDomain);

        SKMatrix? shadingMatrix = shading.Matrix;

        // Concatenate with shading.Matrix if present
        SKMatrix finalMatrix = (shadingMatrix.HasValue)
            ? SKMatrix.Concat(shadingMatrix.Value, pixelToDomain)
            : pixelToDomain;

        return new FunctionShadingResult(bitmap, finalMatrix);
    }
}
