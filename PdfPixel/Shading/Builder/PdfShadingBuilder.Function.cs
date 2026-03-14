using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Functions;
using PdfPixel.Rendering.Operators;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;
using PdfPixel.Text;
using SkiaSharp;
using System;

namespace PdfPixel.Shading;

internal partial class PdfShadingBuilder
{
    /// <summary>
    /// Builds a PDF-spec compliant function-based (Type 1) shading command using SKBitmap.
    /// Samples the function(s) over the domain rectangle using function-provided sampling points.
    /// </summary>
    /// <param name="processor">The command processor that receives generated commands.</param>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="state">Current graphics state.</param>
    /// <returns><see langword="true"/> if commands were produced; otherwise <see langword="false"/>.</returns>
    private void BuildFunctionBasedCommand(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state)
    {
        if (shading.Functions == null || shading.Functions.Count == 0 || shading.ColorSpaceConverter == null)
        {
            _logger.LogWarning("Function-based shading has no functions or color space converter");
            return;
        }

        var converter = state.Page.Cache.ColorSpace.ResolveByObject(shading.ColorSpaceConverter);
        PdfFunction function = shading.Functions[0];

        float domainX0 = 0f;
        float domainX1 = 1f;
        float domainY0 = 0f;
        float domainY1 = 1f;
        if (shading.Domain != null && shading.Domain.Length >= 4)
        {
            domainX0 = shading.Domain[0];
            domainX1 = shading.Domain[1];
            domainY0 = shading.Domain[2];
            domainY1 = shading.Domain[3];
        }

        float domainWidth = Math.Abs(domainX1 - domainX0);
        float domainHeight = Math.Abs(domainY1 - domainY0);
        if (domainWidth < 1e-6f || domainHeight < 1e-6f)
        {
            _logger.LogWarning("Function-based shading has degenerate domain dimensions");
            return;
        }

        int sampleCount = state.RenderingParameters.PreviewMode
            ? state.RenderingParameters.PreviewModeFunctionSamples
            : state.RenderingParameters.DefaultFunctionSamples;

        float[] xSamples = function.GetSamplingPoints(0, domainX0, domainX1, sampleCount);
        float[] ySamples = function.GetSamplingPoints(1, domainY0, domainY1, sampleCount);

        int bitmapWidth = Math.Max(1, xSamples.Length);
        int bitmapHeight = Math.Max(1, ySamples.Length);

        var bitmap = new SKBitmap(bitmapWidth, bitmapHeight);
        SKColor[] pixelColors = new SKColor[bitmapWidth * bitmapHeight];
        for (int yIndex = 0; yIndex < bitmapHeight; yIndex++)
        {
            float domainY = ySamples[yIndex];
            for (int xIndex = 0; xIndex < bitmapWidth; xIndex++)
            {
                float domainX = xSamples[xIndex];
                var comps = function.Evaluate([domainX, domainY]);
                SKColor color = converter.ToSrgb(comps, state.RenderingIntent, state.FullTransferFunction);
                pixelColors[yIndex * bitmapWidth + xIndex] = color;
            }
        }

        bitmap.Pixels = pixelColors;

        // Compute matrix to map bitmap pixel space to domain rectangle
        float scaleX = domainWidth / bitmapWidth;
        float scaleY = domainHeight / bitmapHeight;
        float translateX = domainX0;
        float translateY = domainY0;
        SKMatrix pixelToDomain = SKMatrix.CreateScale(scaleX, scaleY);
        pixelToDomain = SKMatrix.Concat(SKMatrix.CreateTranslation(translateX, translateY), pixelToDomain);

        var matrixArray = shading.SourceObject.Dictionary.GetArray(PdfTokens.MatrixKey);
        var matrix = PdfLocationUtilities.CreateMatrix(matrixArray);

        // Concatenate with shading.Matrix if present
        SKMatrix finalMatrix = matrix.HasValue
            ? SKMatrix.Concat(matrix.Value, pixelToDomain)
            : pixelToDomain;

        processor.Process(new SaveStateCommand());
        processor.Process(new ConcatMatrixCommand(finalMatrix));
        processor.Process(new DrawBitmapCommand(bitmap));
        processor.Process(new RestoreStateCommand());
    }
}
