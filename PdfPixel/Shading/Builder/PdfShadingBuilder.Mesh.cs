using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Builder;
using PdfPixel.Shading.Decoding;
using PdfPixel.Shading.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Shading;

/// <summary>
/// Provides mesh patch rendering for PDF Type 4–7 shading using SkiaSharp.
/// </summary>
internal partial class PdfShadingBuilder
{
    /// <summary>
    /// Builds a command for Gouraud-shaded triangle mesh (Type 4 and Type 5).
    /// </summary>
    /// <param name="processor">The command processor that receives generated commands.</param>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="state">Current graphics state.</param>
    /// <returns><see langword="true"/> if commands were produced; otherwise <see langword="false"/>.</returns>
    private void BuildGouraudCommand(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state)
    {
        var decoder = new GouraudMeshDecoder(shading, state);
        List<MeshData> triangles = decoder.Decode();
        if (triangles.Count == 0)
        {
            _logger.LogWarning("Gouraud mesh shading produced no triangles");
            return;
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

        var paint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias, state);

        // Batch draw all triangles in one call
        var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, allPoints, allColors);

        processor.Process(new DrawVerticesCommand(vertices, paint));
    }

    /// <summary>
    /// Builds a command for type 7 (Tensor-Product Patch Mesh).
    /// </summary>
    /// <param name="processor">The command processor that receives generated commands.</param>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="state">Current graphics state.</param>
    /// <returns><see langword="true"/> if commands were produced; otherwise <see langword="false"/>.</returns>
    private void BuildType7Command(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state)
    {
        BuildPatchMeshCommand(processor, shading, state);
    }

    /// <summary>
    /// Builds a command for type 6 (Coons Patch Mesh) shading.
    /// </summary>
    /// <param name="processor">The command processor that receives generated commands.</param>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="state">Current graphics state.</param>
    /// <returns><see langword="true"/> if commands were produced; otherwise <see langword="false"/>.</returns>
    private void BuildType6Command(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state)
    {
        BuildPatchMeshCommand(processor, shading, state);
    }

    /// <summary>
    /// Universal command builder for both type 6 and 7 meshes.
    /// </summary>
    /// <param name="processor">The command processor that receives generated commands.</param>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="state">Current graphics state.</param>
    /// <returns><see langword="true"/> if commands were produced; otherwise <see langword="false"/>.</returns>
    private void BuildPatchMeshCommand(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state)
    {
        var decoder = new MeshDecoder(shading, state);
        List<MeshData> patches = decoder.Decode();
        if (patches.Count == 0)
        {
            _logger.LogWarning("Patch mesh shading produced no patches");
            return;
        }

        var paint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias, state);

        int verticesPerPatch = state.RenderingParameters.PreviewMode
            ? state.RenderingParameters.PreviewMaxTessellationVertices
            : state.RenderingParameters.MaxTessellationVertices;

        var vertices = MeshEvaluator.CreateVerticesForPatches(patches, verticesPerPatch);

        processor.Process(new DrawVerticesCommand(vertices, paint));
    }
}
