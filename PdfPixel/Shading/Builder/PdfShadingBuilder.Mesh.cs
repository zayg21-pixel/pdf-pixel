using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.Sampling;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Shading.Builder;
using PdfPixel.Shading.Decoding;
using PdfPixel.Shading.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Shading;

/// <summary>
/// Provides mesh patch rendering for PDF Type 4–7 shading.
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
    public PdfVertices? BuildGouraudVertices(PdfShading shading, ColorTransformSampler sampler)
    {
        GouraudMeshDecoder decoder = new(shading, sampler);
        List<MeshData> triangles = decoder.Decode();
        if (triangles.Count == 0)
        {
            _logger.LogWarning("Gouraud mesh shading produced no triangles");
            return null;
        }

        // Aggregate all triangle points and colors into single arrays for batch drawing
        int triangleCount = triangles.Count;
        int vertexCount = triangleCount * 3;
        var allPoints = new PdfPoint[vertexCount];
        var allColors = new PdfColor[vertexCount];

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            MeshData triangle = triangles[triangleIndex];
            int offset = triangleIndex * 3;

            Array.Copy(triangle.Points, 0, allPoints, offset, 3);
            Array.Copy(triangle.CornerColors, 0, allColors, offset, 3);
        }

        return new PdfVertices(allPoints, allColors, null);
    }

    /// <summary>
    /// Builds patch mesh vertices for type 6 (Coons) and type 7 (Tensor-product) shadings.
    /// Returns null if no patches are decoded.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="sampler">RGBA sampler for color conversion.</param>
    /// <param name="maxTessellationVertices">Maximum tessellation vertices per patch.</param>
    /// <param name="observer">Execution observer for long-running operations.</param>
    /// <returns>Tessellated patch vertices, or null on failure.</returns>
    public PdfVertices? BuildPatchMeshVertices(PdfShading shading, ColorTransformSampler sampler, int maxTessellationVertices, IPdfExecutionObserver observer)
    {
        MeshDecoder decoder = new(shading, sampler);
        List<MeshData> patches = decoder.Decode();
        if (patches.Count == 0)
        {
            _logger.LogWarning("Patch mesh shading produced no patches");
            return null;
        }

        return MeshEvaluator.CreateVerticesForPatches(patches, maxTessellationVertices, observer);
    }
}
