using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.Sampling;
using PdfPixel.Geometry;
using PdfPixel.Models;
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
    /// Builds the vertices of any mesh shading: a Gouraud-shaded triangle mesh (Type 4 and Type 5)
    /// or a patch mesh (Type 6 and Type 7).
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="colorResolver">Resolver for the color of an interpolated parametric value.</param>
    /// <param name="maxTessellationVertices">Maximum subdivisions per triangle edge or patch axis.</param>
    /// <param name="observer">Execution observer for long-running operations.</param>
    /// <returns>Batched vertices, or null when the shading's stream decodes to nothing.</returns>
    public PdfVertices? BuildMeshVertices(
        PdfShading shading,
        MeshColorResolver colorResolver,
        int maxTessellationVertices,
        IPdfExecutionObserver? observer)
    {
        switch (shading.ShadingType)
        {
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            {
                return BuildGouraudVertices(shading, colorResolver, maxTessellationVertices, observer);
            }
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                return BuildPatchMeshVertices(shading, colorResolver, maxTessellationVertices, observer);
            }
            default:
            {
                throw new ArgumentException($"Not a mesh shading type {shading.ShadingType}");
            }
        }
    }

    /// <summary>
    /// Builds Gouraud-shaded triangle mesh vertices (Type 4 and Type 5).
    /// Returns null if no triangles are decoded.
    /// </summary>
    private PdfVertices? BuildGouraudVertices(
        PdfShading shading,
        MeshColorResolver colorResolver,
        int maxTessellationVertices,
        IPdfExecutionObserver? observer)
    {
        GouraudMeshDecoder decoder = new(shading, colorResolver);
        List<MeshData> triangles = decoder.Decode();
        if (triangles.Count == 0)
        {
            _logger.LogWarning("Gouraud mesh shading produced no triangles");
            return null;
        }

        return MeshEvaluator.CreateVerticesForTriangles(triangles, colorResolver, maxTessellationVertices, observer);
    }

    /// <summary>
    /// Builds patch mesh vertices for type 6 (Coons) and type 7 (Tensor-product) shadings.
    /// Returns null if no patches are decoded.
    /// </summary>
    private PdfVertices? BuildPatchMeshVertices(
        PdfShading shading,
        MeshColorResolver colorResolver,
        int maxTessellationVertices,
        IPdfExecutionObserver? observer)
    {
        MeshDecoder decoder = new(shading, colorResolver);
        List<MeshData> patches = decoder.Decode();
        if (patches.Count == 0)
        {
            _logger.LogWarning("Patch mesh shading produced no patches");
            return null;
        }

        return MeshEvaluator.CreateVerticesForPatches(patches, colorResolver, maxTessellationVertices, observer);
    }
}
