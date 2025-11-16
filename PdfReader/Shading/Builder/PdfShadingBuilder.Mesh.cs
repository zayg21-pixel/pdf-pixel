using PdfReader.Color.Paint;
using PdfReader.Shading.Builder;
using PdfReader.Shading.Decoding;
using PdfReader.Shading.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Shading;

/// <summary>
/// Provides mesh patch rendering for PDF Type 6 and Type 7 shading using SkiaSharp and SkSL runtime effects.
/// </summary>
internal static partial class PdfShadingBuilder
{
    private const int MaxTessellationVertices = 32;

    /// <summary>
    /// Builds an SKShader for Gouraud-shaded triangle mesh (Type 4 and Type 5) shading using SkiaSharp.
    /// Uses GouraudMeshDecoder to extract triangles and batches all triangles into a single SKVertices draw call for performance.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <returns>SKShader instance or null if decoding or rendering fails.</returns>
    private static SKShader BuildGouraud(PdfShading shading)
    {
        var decoder = new GouraudMeshDecoder(shading);
        List<MeshData> triangles = decoder.Decode();
        if (triangles.Count == 0)
        {
            return null;
        }

        SKRect meshBounds = ComputeMeshBounds(triangles);
        var normalizedMeshBounds = new SKRect(0, 0, meshBounds.Width, meshBounds.Height);

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
        using var canvas = recorder.BeginRecording(normalizedMeshBounds);
        canvas.Translate(-meshBounds.Left, -meshBounds.Top);

        using var paint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias);

        // Batch draw all triangles in one call
        using var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, allPoints, allColors);
        canvas.DrawVertices(vertices, SKBlendMode.Modulate, paint);

        using var picture = recorder.EndRecording();

        var localMatrix = SKMatrix.CreateTranslation(meshBounds.Left, meshBounds.Top);

        return picture.ToShader(SKShaderTileMode.Decal, SKShaderTileMode.Decal, SKFilterMode.Linear, localMatrix, normalizedMeshBounds);
    }

    /// <summary>
    /// Builds an SKShader for type 7 (Tensor-Product Patch Mesh) shading using SkSL and a data texture.
    /// All mesh patches are packed into a single texture, each as a 4x4 block.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <returns>SKShader instance or null if decoding or rendering fails.</returns>
    private static SKShader BuildType7(PdfShading shading)
    {
        var decoder = new MeshDecoder(shading);
        List<MeshData> patches = decoder.Decode();
        if (patches.Count == 0)
        {
            return null;
        }

        SKRect meshBounds = ComputeMeshBounds(patches);
        var normalizedMeshBounds = new SKRect(0, 0, meshBounds.Width, meshBounds.Height);

        var area = meshBounds.Width * meshBounds.Height;
        var maxCount = MathF.Sqrt(area / patches.Count * MathF.Sqrt(3) / 4f); // average triangle side size in pixels
        if (maxCount < 0)
        {
            maxCount = 1;
        }

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(normalizedMeshBounds);
        canvas.Translate(-meshBounds.Left, -meshBounds.Top);

        int patchCount = patches.Count;
        int tessellation = (int)MathF.Round(Math.Min(maxCount, MaxTessellationVertices));

        using var paint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias);

        using var vertices = MeshEvaluator.CreateVerticesForPatches(patches, tessellation);
        canvas.DrawVertices(vertices, SKBlendMode.Modulate, paint);

        using var picture = recorder.EndRecording();

        var localMatrix = SKMatrix.CreateTranslation(meshBounds.Left, meshBounds.Top);

        return picture.ToShader(SKShaderTileMode.Decal, SKShaderTileMode.Decal, SKFilterMode.Nearest, localMatrix, normalizedMeshBounds);
    }

    /// <summary>
    /// Builds an SKShader for type 6 (Coons Patch Mesh) shading using SkiaSharp.
    /// Uses MeshDecoder to extract patches, each with 12 control points.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <returns>SKShader instance or null if decoding or rendering fails.</returns>
    private static SKShader BuildType6(PdfShading shading)
    {
        var decoder = new MeshDecoder(shading);
        var patches = decoder.Decode();
        if (patches.Count == 0)
        {
            return null;
        }

        SKRect meshBounds = ComputeMeshBounds(patches);
        var normalizedMeshBounds = new SKRect(0, 0, meshBounds.Width, meshBounds.Height);

        using var paint = PdfPaintFactory.CreateShaderPaint(shading.AntiAlias);

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(normalizedMeshBounds);
        canvas.Translate(-meshBounds.Left, -meshBounds.Top);

        foreach (var patch in patches)
        {
            var controlPoints = patch.Points;
            Array.Resize(ref controlPoints, 12);

            canvas.DrawPatch(controlPoints, patch.CornerColors, null, paint);
        }

        using var picture = recorder.EndRecording();

        var localMatrix = SKMatrix.CreateTranslation(meshBounds.Left, meshBounds.Top);

        return SKShader.CreatePicture(picture, SKShaderTileMode.Decal, SKShaderTileMode.Decal, SKFilterMode.Linear, localMatrix, normalizedMeshBounds);
    }

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
