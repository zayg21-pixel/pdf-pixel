using PdfReader.Color.Paint;
using PdfReader.Rendering.State;
using PdfReader.Shading.Builder;
using PdfReader.Shading.Decoding;
using PdfReader.Shading.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfReader.Shading;

/// <summary>
/// Provides mesh patch rendering for PDF Type 6 and Type 7 shading using SkiaSharp and SkSL runtime effects.
/// </summary>
internal static partial class PdfShadingBuilder
{
    private const int MaxTessellationVertices = 32;

    /// <summary>
    /// Builds an SKPicture for Gouraud-shaded triangle mesh (Type 4 and Type 5).
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="state">Current graphics state.</param>
    /// <returns>SKShader instance or null if decoding or rendering fails.</returns>
    private static SKPicture BuildGouraud(PdfShading shading, PdfGraphicsState state)
    {
        var decoder = new GouraudMeshDecoder(shading, state);
        List<MeshData> triangles = decoder.Decode();
        if (triangles.Count == 0)
        {
            return null;
        }

        SKRect meshBounds = ComputeMeshBounds(triangles);

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

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(meshBounds);

        using var paint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias);

        // Batch draw all triangles in one call
        using var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, allPoints, allColors);
        canvas.DrawVertices(vertices, SKBlendMode.DstIn, paint);

        return recorder.EndRecording();
    }
    /// <summary>
    /// Builds an SKPicture for type 7 (Tensor-Product Patch Mesh).
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="state">Current graphics state.</param>
    /// <returns>SKShader instance or null if decoding or rendering fails.</returns>
    private static SKPicture BuildType7(PdfShading shading, PdfGraphicsState state)
    {
        var decoder = new MeshDecoder(shading, state);
        List<MeshData> patches = decoder.Decode();
        if (patches.Count == 0)
        {
            return null;
        }

        SKRect meshBounds = ComputeMeshBounds(patches);

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(meshBounds);

        using var paint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias);

        using var vertices = MeshEvaluator.CreateVerticesForPatches(patches, MaxTessellationVertices);
        canvas.DrawVertices(vertices, SKBlendMode.DstIn, paint);

        return recorder.EndRecording();
    }

    /// <summary>
    /// Builds an SKPicture for type 6 (Coons Patch Mesh) shading using SkiaSharp.
    /// Uses MeshDecoder to extract patches, each with 12 control points.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="state">Current graphics state.</param>
    /// <returns>SKShader instance or null if decoding or rendering fails.</returns>
    private static SKPicture BuildType6(PdfShading shading, PdfGraphicsState state)
    {
        var decoder = new MeshDecoder(shading, state);
        var patches = decoder.Decode();
        if (patches.Count == 0)
        {
            return null;
        }

        SKRect meshBounds = ComputeMeshBounds(patches);

        using var paint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias);

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(meshBounds);

        foreach (var patch in patches)
        {
            var controlPoints = patch.Points;
            Array.Resize(ref controlPoints, 12);

            canvas.DrawPatch(controlPoints, patch.CornerColors, null, SKBlendMode.DstIn, paint);
        }

        return recorder.EndRecording();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKRect ComputeMeshBounds(List<MeshData> patches)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (var patch in patches)
        {
            foreach (var point in patch.Points)
            {
                if (point.X < minX)
                {
                    minX = point.X;
                }
                if (point.Y < minY)
                {
                    minY = point.Y;
                }
                if (point.X > maxX)
                {
                    maxX = point.X;
                }
                if (point.Y > maxY)
                {
                    maxY = point.Y;
                }
            }
        }
        return new SKRect(minX, minY, maxX, maxY);
    }
}
