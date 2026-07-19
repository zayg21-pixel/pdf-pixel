using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Geometry;
using PdfPixel.Parsing;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Shading.Decoding;

/// <summary>
/// Provides static helpers for reading mesh points and color components from PDF shading streams.
/// </summary>
internal static class MeshReader
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPoint ReadPoint(
        ref UintBitReader bitReader,
        int bitsPerCoordinate,
        float xmin,
        float ymin,
        float xScale,
        float yScale)
    {
        uint rawX = bitReader.ReadBits(bitsPerCoordinate);
        uint rawY = bitReader.ReadBits(bitsPerCoordinate);
        float decodedX = xmin + (rawX * xScale);
        float decodedY = ymin + (rawY * yScale);
        return new PdfPoint(decodedX, decodedY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfColor ReadColorComponents(
        ref UintBitReader bitReader,
        int bitsPerComponent,
        ColorMinAndScale[] colorComponentMinAndScale,
        int numColorComponents,
        List<PdfFunction> functions,
        ColorTransformSampler colorSampler)
    {
        var components = new float[numColorComponents];
        for (int componentIndex = 0; componentIndex < numColorComponents; componentIndex++)
        {
            uint rawValue = bitReader.ReadBits(bitsPerComponent);
            ColorMinAndScale minAndScale = colorComponentMinAndScale[componentIndex];
            float decoded = minAndScale.Min + (rawValue * minAndScale.Scale);
            components[componentIndex] = decoded;
        }

        return EvaluatePatchColor(components, functions, colorSampler);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfColor EvaluatePatchColor(
        in ReadOnlySpan<float> input,
        List<PdfFunction> functions,
        ColorTransformSampler colorSampler)
    {
        if (functions?.Count > 0)
        {
            ReadOnlySpan<float> evaluated = PdfFunctions.EvaluateColorFunctions(functions, input);
            return colorSampler.Sample(evaluated).ToPdfColor();
        }
        else
        {
            return colorSampler.Sample(input).ToPdfColor();
        }
    }
}
