using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Functions;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Rendering.Operators;
using PdfPixel.Text;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Shading.Builder;

internal partial class PdfShadingBuilder
{
    private const int FloatBitsPerComponent = 32;

    /// <summary>
    /// Builds a function-based (Type 1) shading image and the matrix that maps
    /// image pixel space into the shading coordinate system.
    /// Returns <see langword="null"/> if the shading is invalid or degenerate.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="palette">Palette the function values index into, or null when the shading's color space is not Indexed.</param>
    /// <param name="componentCount">Number of color components each sample holds.</param>
    /// <param name="defaultFunctionSamples">Number of function samples to use.</param>
    /// <param name="observer">Execution observer for progress and cancellation.</param>
    /// <returns>A <see cref="PdfShadingImage"/> containing the samples and matrix, or <see langword="null"/> on failure.</returns>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public PdfShadingImage? BuildFunctionBased(
        PdfShading shading,
        PdfPalette? palette,
        int componentCount,
        int defaultFunctionSamples,
        IPdfExecutionObserver? observer)
    {
        if (shading.Functions == null || shading.Functions.Length == 0 || !shading.ColorSpaceReference.IsPresent || componentCount <= 0)
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

        var channels = new PdfImageChannel[componentCount];
        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            channels[componentIndex] = PdfImageChannel.Component(componentIndex);
        }

        PdfImageSamples samples = new(bitmapWidth, bitmapHeight, PdfImageSampleFormat.Float, FloatBitsPerComponent, channels);

        for (int yIndex = 0; yIndex < bitmapHeight; yIndex++)
        {
            float domainY = ySamples[yIndex];
            Span<float> row = MemoryMarshal.Cast<byte, float>(samples.GetRow(yIndex));

            for (int xIndex = 0; xIndex < bitmapWidth; xIndex++)
            {
                float domainX = xSamples[xIndex];
                ReadOnlySpan<float> components = function.Evaluate([domainX, domainY]);

                if (palette != null)
                {
                    components = palette.GetColor((components.Length > 0) ? components[0] : 0f).Components.Span;
                }

                Span<float> destination = row.Slice(xIndex * componentCount, componentCount);
                int copiedCount = Math.Min(components.Length, componentCount);
                components.Slice(0, copiedCount).CopyTo(destination);
                destination.Slice(copiedCount).Clear();
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

        return new PdfShadingImage(samples, finalMatrix);
    }
}
