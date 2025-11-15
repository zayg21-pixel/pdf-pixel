using PdfReader.Functions;
using PdfReader.Shading.Model;
using SkiaSharp;
using System;

namespace PdfReader.Shading;

internal static partial class PdfShadingBuilder
{
    /// <summary>
    /// Builds a PDF-spec compliant function-based (Type 1) shading shader using SKBitmap.
    /// If the function is sampled, uses the sample grid size mapped to shading.Domain.
    /// Otherwise, samples the function(s) over the domain rectangle at a fixed resolution.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <returns>SKShader instance or null if input is invalid.</returns>
    private static SKShader BuildFunctionBased(PdfShading shading)
    {
        if (shading.Functions == null || shading.Functions.Count == 0 || shading.ColorSpaceConverter == null)
        {
            return null;
        }

        var converter = shading.ColorSpaceConverter;
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
            return null;
        }

        int bitmapWidth;
        int bitmapHeight;

        if (function is SampledPdfFunction sampled)
        {
            bitmapWidth = sampled.Dimensions > 0 ? sampled.Sizes[0] : 1;
            bitmapHeight = sampled.Dimensions > 1 ? sampled.Sizes[1] : 1;
        }
        else
        {
            const int DefaultResolution = 128;
            bitmapWidth = DefaultResolution;
            bitmapHeight = (int)(DefaultResolution * domainHeight / domainWidth);
            if (bitmapHeight < 2)
            {
                bitmapHeight = 2;
            }
        }

        using var bitmap = new SKBitmap(bitmapWidth, bitmapHeight);
        SKColor[] pixelColors = new SKColor[bitmapWidth * bitmapHeight];
        for (int yIndex = 0; yIndex < bitmapHeight; yIndex++)
        {
            float fy = yIndex / (float)(bitmapHeight - 1);
            float domainY = domainY0 + fy * (domainY1 - domainY0);
            for (int xIndex = 0; xIndex < bitmapWidth; xIndex++)
            {
                float fx = xIndex / (float)(bitmapWidth - 1);
                float domainX = domainX0 + fx * (domainX1 - domainX0);
                var comps = function.Evaluate([domainX, domainY]);
                SKColor color = converter.ToSrgb(comps, shading.RenderingIntent);
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

        // Concatenate with shading.Matrix if present
        SKMatrix finalMatrix = shading.Matrix.HasValue
            ? SKMatrix.Concat(shading.Matrix.Value, pixelToDomain)
            : pixelToDomain;

        return bitmap.ToShader(
            SKShaderTileMode.Decal,
            SKShaderTileMode.Decal,
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
            finalMatrix);
    }
}
