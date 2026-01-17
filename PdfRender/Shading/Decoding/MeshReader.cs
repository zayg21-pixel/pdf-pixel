using PdfRender.Color.ColorSpace;
using PdfRender.Color.Sampling;
using PdfRender.Color.Transform;
using PdfRender.Functions;
using PdfRender.Parsing;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfRender.Shading.Decoding;

/// <summary>
/// Provides static helpers for reading mesh points and color components from PDF shading streams.
/// </summary>
internal static class MeshReader
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPoint ReadPoint(
        ref UintBitReader bitReader,
        int bitsPerCoordinate,
        float xmin,
        float ymin,
        float xScale,
        float yScale)
    {
        uint rawX = bitReader.ReadBits(bitsPerCoordinate);
        uint rawY = bitReader.ReadBits(bitsPerCoordinate);
        float decodedX = xmin + rawX * xScale;
        float decodedY = ymin + rawY * yScale;
        return new SKPoint(decodedX, decodedY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor ReadColorComponents(
        ref UintBitReader bitReader,
        int bitsPerComponent,
        ColorMinAndScale[] colorComponentMinAndScale,
        int numColorComponents,
        List<PdfFunction> functions,
        IRgbaSampler colorSampler)
    {
        var components = new float[numColorComponents];
        for (int componentIndex = 0; componentIndex < numColorComponents; componentIndex++)
        {
            uint rawValue = bitReader.ReadBits(bitsPerComponent);
            ColorMinAndScale minAndScale = colorComponentMinAndScale[componentIndex];
            float decoded = minAndScale.Min + rawValue * minAndScale.Scale;
            components[componentIndex] = decoded;
        }
        return EvaluatePatchColor(components, functions, colorSampler);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor EvaluatePatchColor(
        ReadOnlySpan<float> input,
        List<PdfFunction> functions,
        IRgbaSampler colorSampler)
    {
        if (functions != null && functions.Count > 0)
        {
            var evaluated = PdfFunctions.EvaluateColorFunctions(functions, input);
            return ColorVectorUtilities.From01ToSkiaColor(colorSampler.Sample(evaluated));
        }
        else
        {
            return ColorVectorUtilities.From01ToSkiaColor(colorSampler.Sample(input));
        }
    }
}
