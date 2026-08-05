using PdfPixel.Geometry;
using PdfPixel.Parsing;
using PdfPixel.Shading.Model;
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

    /// <summary>
    /// Reads one vertex's color data and resolves it through <paramref name="colorResolver"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MeshVertexColor ReadColorComponents(
        ref UintBitReader bitReader,
        int bitsPerComponent,
        ColorMinAndScale[] colorComponentMinAndScale,
        int numColorComponents,
        MeshColorResolver colorResolver)
    {
        var components = new float[numColorComponents];
        for (int componentIndex = 0; componentIndex < numColorComponents; componentIndex++)
        {
            uint rawValue = bitReader.ReadBits(bitsPerComponent);
            ColorMinAndScale minAndScale = colorComponentMinAndScale[componentIndex];
            float decoded = minAndScale.Min + (rawValue * minAndScale.Scale);
            components[componentIndex] = decoded;
        }

        return colorResolver.CreateVertexColor(components);
    }
}
