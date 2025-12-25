using PdfReader.Color.ColorSpace;
using PdfReader.Functions;
using PdfReader.Parsing;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfReader.Shading.Decoding;

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
        PdfColorSpaceConverter colorSpace,
        PdfRenderingIntent intent)
    {
        var components = new float[numColorComponents];
        for (int componentIndex = 0; componentIndex < numColorComponents; componentIndex++)
        {
            uint rawValue = bitReader.ReadBits(bitsPerComponent);
            ColorMinAndScale minAndScale = colorComponentMinAndScale[componentIndex];
            float decoded = minAndScale.Min + rawValue * minAndScale.Scale;
            components[componentIndex] = decoded;
        }
        return EvaluatePatchColor(components, functions, colorSpace, intent);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor EvaluatePatchColor(
        ReadOnlySpan<float> input,
        List<PdfFunction> functions,
        PdfColorSpaceConverter colorSpace,
        PdfRenderingIntent intent)
    {
        if (functions != null && functions.Count > 0)
        {
            var evaluated = PdfFunctions.EvaluateColorFunctions(functions, input);
            return colorSpace.ToSrgb(evaluated, intent);
        }
        else
        {
            return colorSpace.ToSrgb(input, intent);
        }
    }

    /// <summary>
    /// Skips padding bits at the end of a record to align to the next byte boundary, per PDF spec.
    /// </summary>
    /// <param name="bitReader">The bit reader to advance.</param>
    /// <param name="bitsRead">The number of bits read in the record.</param>
    public static void SkipPaddingBits(ref UintBitReader bitReader, int bitsRead)
    {
        int bitsToPad = (8 - bitsRead % 8) % 8;
        if (bitsToPad > 0)
        {
            bitReader.ReadBits(bitsToPad);
        }
    }
}
