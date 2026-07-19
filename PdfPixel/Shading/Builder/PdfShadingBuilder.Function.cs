using Microsoft.Extensions.Logging;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using PdfPixel.Commands;
using PdfPixel.Functions;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Rendering.Operators;
using PdfPixel.Shading.Model;
using PdfPixel.Text;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Shading;

internal partial class PdfShadingBuilder
{
    /// <summary>
    /// Builds a function-based (Type 1) shading image and the matrix that maps
    /// image pixel space into the shading coordinate system.
    /// Returns <see langword="null"/> if the shading is invalid or degenerate.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="sampler">RGBA sampler for color conversion.</param>
    /// <param name="defaultFunctionSamples">Number of function samples to use.</param>
    /// <param name="observer">Execution observer for progress and cancellation.</param>
    /// <returns>A <see cref="FunctionShadingResult"/> containing the image and matrix, or <see langword="null"/> on failure.</returns>
    public FunctionShadingResult? BuildFunctionBased(
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

        PdfDecodedImage decodedImage = new(bitmapWidth, bitmapHeight, PdfImageColorFormat.Rgba);
        Span<byte> imageBuffer = decodedImage.GetRawBuffer();
        ref RgbaPacked destPixel = ref Unsafe.As<byte, RgbaPacked>(ref imageBuffer[0]);

        for (int yIndex = 0; yIndex < bitmapHeight; yIndex++)
        {
            float domainY = ySamples[yIndex];
            for (int xIndex = 0; xIndex < bitmapWidth; xIndex++)
            {
                float domainX = xSamples[xIndex];
                ReadOnlySpan<float> comps = function.Evaluate([domainX, domainY]);
                Vector4 colorVector = sampler.Sample(comps);
                ColorVectorUtilities.Load01ToRgba(colorVector, ref destPixel);
                destPixel = ref Unsafe.Add(ref destPixel, 1);
            }

            observer?.Notify();
        }

        // Compute matrix to map bitmap pixel space to domain rectangle
        float scaleX = domainWidth / bitmapWidth;
        float scaleY = domainHeight / bitmapHeight;
        float translateX = domainX0;
        float translateY = domainY0;
        PdfMatrix pixelToDomain = PdfMatrix.CreateScale(scaleX, scaleY);
        pixelToDomain = PdfMatrix.Concat(PdfMatrix.CreateTranslation(translateX, translateY), pixelToDomain);

        PdfMatrix? shadingMatrix = shading.Matrix;

        // Concatenate with shading.Matrix if present
        PdfMatrix finalMatrix = (shadingMatrix.HasValue)
            ? PdfMatrix.Concat(shadingMatrix.Value, pixelToDomain)
            : pixelToDomain;

        return new FunctionShadingResult(decodedImage, finalMatrix);
    }
}
