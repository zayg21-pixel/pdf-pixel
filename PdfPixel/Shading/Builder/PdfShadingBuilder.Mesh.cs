using Microsoft.Extensions.Logging;
using PdfPixel.Color.Sampling;
using PdfPixel.Shading.Builder;
using PdfPixel.Shading.Decoding;
using PdfPixel.Shading.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Shading;

/// <summary>
/// Provides mesh patch rendering for PDF Type 4–7 shading using SkiaSharp.
/// </summary>
internal partial class PdfShadingBuilder
{
    /// <summary>
    /// Builds Gouraud-shaded triangle mesh vertices (Type 4 and Type 5).
    /// Returns null if no triangles are decoded.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="sampler">RGBA sampler for color conversion.</param>
    /// <returns>Batched triangle vertices, or null on failure.</returns>
    public SKVertices BuildGouraudVertices(PdfShading shading, IRgbaSampler sampler)
    {
        var decoder = new GouraudMeshDecoder(shading, sampler);
        List<MeshData> triangles = decoder.Decode();
        if (triangles.Count == 0)
        {
            _logger.LogWarning("Gouraud mesh shading produced no triangles");
            return null;
        }

        // Aggregate all triangle points and colors into single arrays for batch drawing
        int triangleCount = triangles.Count;
        int vertexCount = triangleCount * 3;
        SKPoint[] allPoints = new SKPoint[vertexCount];
        SKColor[] allColors = new SKColor[vertexCount];

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            MeshData triangle = triangles[triangleIndex];
            Array.Copy(triangle.Points, 0, allPoints, triangleIndex * 3, 3);
            Array.Copy(triangle.CornerColors, 0, allColors, triangleIndex * 3, 3);
        }

        return SKVertices.CreateCopy(SKVertexMode.Triangles, allPoints, allColors);
    }

    /// <summary>
    /// Builds patch mesh vertices for type 6 (Coons) and type 7 (Tensor-product) shadings.
    /// Returns null if no patches are decoded.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="sampler">RGBA sampler for color conversion.</param>
    /// <param name="maxTessellationVertices">Maximum tessellation vertices per patch.</param>
    /// <returns>Tessellated patch vertices, or null on failure.</returns>
    public SKVertices BuildPatchMeshVertices(PdfShading shading, IRgbaSampler sampler, int maxTessellationVertices)
    {
        var decoder = new MeshDecoder(shading, sampler);
        List<MeshData> patches = decoder.Decode();
        if (patches.Count == 0)
        {
            _logger.LogWarning("Patch mesh shading produced no patches");
            return null;
        }

        return MeshEvaluator.CreateVerticesForPatches(patches, maxTessellationVertices);
    }
}
