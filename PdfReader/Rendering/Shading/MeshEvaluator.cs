using SkiaSharp;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System;

namespace PdfReader.Rendering.Shading
{
    /// <summary>
    /// Provides static methods for evaluating tensor-product Bézier surfaces and interpolating corner colors for mesh patches.
    /// </summary>
    internal static class MeshEvaluator
    {
        // Spiral index mapping for Type 7 tensor patch control points
        private const int P00 = 0;   // (0,0)
        private const int P10 = 11;  // (1,0)
        private const int P20 = 10;  // (2,0)
        private const int P30 = 9;   // (3,0)
        private const int P01 = 1;   // (0,1)
        private const int P11 = 12;  // (1,1)
        private const int P21 = 14;  // (2,1)
        private const int P31 = 8;   // (3,1)
        private const int P02 = 2;   // (0,2)
        private const int P12 = 13;  // (1,2)
        private const int P22 = 15;  // (2,2)
        private const int P32 = 7;   // (3,2)
        private const int P03 = 3;   // (0,3)
        private const int P13 = 4;   // (1,3)
        private const int P23 = 5;   // (2,3)
        private const int P33 = 6;   // (3,3)

        /// <summary>
        /// Creates tessellated vertices for a Type 7 mesh patch as an SKVertices instance.
        /// </summary>
        /// <param name="controlPoints">Array of 16 control points for the tensor patch.</param>
        /// <param name="cornerColors">Array of 4 corner colors for the patch.</param>
        /// <param name="tessellation">Number of subdivisions per axis (higher = smoother).</param>
        /// <returns>SKVertices instance containing tessellated mesh vertices, colors, and indices.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SKVertices CreateVertices(SKPoint[] controlPoints, SKColor[] cornerColors, int tessellation)
        {
            if (controlPoints == null || controlPoints.Length != 16)
            {
                throw new ArgumentException("controlPoints must have 16 elements.");
            }
            if (cornerColors == null || cornerColors.Length != 4)
            {
                throw new ArgumentException("cornerColors must have 4 elements.");
            }
            if (tessellation < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tessellation), "Tessellation must be >= 1.");
            }

            int vertexCountPerAxis = tessellation + 1;
            int totalVertices = vertexCountPerAxis * vertexCountPerAxis;
            int totalQuads = tessellation * tessellation;
            int totalIndices = totalQuads * 6;

            SKPoint[] vertices = new SKPoint[totalVertices];
            SKColor[] colors = new SKColor[totalVertices];
            ushort[] indices = new ushort[totalIndices];

            int vertexIndex = 0;
            for (int rowIndex = 0; rowIndex < vertexCountPerAxis; rowIndex++)
            {
                float v = (float)rowIndex / tessellation;
                for (int columnIndex = 0; columnIndex < vertexCountPerAxis; columnIndex++)
                {
                    float u = (float)columnIndex / tessellation;
                    vertices[vertexIndex] = EvalTensorBezier(u, v, controlPoints);
                    colors[vertexIndex] = InterpolateCornerColors(u, v, cornerColors);
                    vertexIndex++;
                }
            }

            int index = 0;
            for (int rowIndex = 0; rowIndex < tessellation; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < tessellation; columnIndex++)
                {
                    int idx0 = rowIndex * vertexCountPerAxis + columnIndex;
                    int idx1 = idx0 + 1;
                    int idx2 = idx0 + vertexCountPerAxis;
                    int idx3 = idx2 + 1;

                    indices[index++] = (ushort)idx0;
                    indices[index++] = (ushort)idx1;
                    indices[index++] = (ushort)idx2;
                    indices[index++] = (ushort)idx1;
                    indices[index++] = (ushort)idx3;
                    indices[index++] = (ushort)idx2;
                }
            }

            return SKVertices.CreateCopy(SKVertexMode.Triangles, vertices, null, colors, indices);
        }

        /// <summary>
        /// Creates tessellated vertices for all Type 7 mesh patches in a single batch for efficient rendering.
        /// </summary>
        /// <param name="patches">List of mesh patches to tessellate.</param>
        /// <param name="tessellation">Number of subdivisions per axis (higher = smoother).</param>
        /// <returns>SKVertices instance containing all tessellated mesh vertices, colors, and indices.</returns>
        public static SKVertices CreateVerticesForPatches(List<MeshData> patches, int tessellation)
        {
            if (patches == null || patches.Count == 0)
            {
                throw new ArgumentException("patches must not be null or empty.");
            }
            if (tessellation < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tessellation), "Tessellation must be >= 1.");
            }

            int vertexCountPerAxis = tessellation + 1;
            int verticesPerPatch = vertexCountPerAxis * vertexCountPerAxis;
            int quadsPerPatch = tessellation * tessellation;
            int indicesPerPatch = quadsPerPatch * 6;

            int totalVertices = verticesPerPatch * patches.Count;
            int totalIndices = indicesPerPatch * patches.Count;

            SKPoint[] allVertices = new SKPoint[totalVertices];
            SKColor[] allColors = new SKColor[totalVertices];
            ushort[] allIndices = new ushort[totalIndices];

            int vertexOffset = 0;
            int indexOffset = 0;
            for (int patchIndex = 0; patchIndex < patches.Count; patchIndex++)
            {
                MeshData patch = patches[patchIndex];
                int vertexIndex = 0;
                for (int rowIndex = 0; rowIndex < vertexCountPerAxis; rowIndex++)
                {
                    float v = (float)rowIndex / tessellation;
                    for (int columnIndex = 0; columnIndex < vertexCountPerAxis; columnIndex++)
                    {
                        float u = (float)columnIndex / tessellation;
                        allVertices[vertexOffset + vertexIndex] = EvalTensorBezier(u, v, patch.Points);
                        allColors[vertexOffset + vertexIndex] = InterpolateCornerColors(u, v, patch.CornerColors);
                        vertexIndex++;
                    }
                }
                int index = 0;
                for (int rowIndex = 0; rowIndex < tessellation; rowIndex++)
                {
                    for (int columnIndex = 0; columnIndex < tessellation; columnIndex++)
                    {
                        int idx0 = vertexOffset + rowIndex * vertexCountPerAxis + columnIndex;
                        int idx1 = idx0 + 1;
                        int idx2 = idx0 + vertexCountPerAxis;
                        int idx3 = idx2 + 1;

                        allIndices[indexOffset + index++] = (ushort)idx0;
                        allIndices[indexOffset + index++] = (ushort)idx1;
                        allIndices[indexOffset + index++] = (ushort)idx2;
                        allIndices[indexOffset + index++] = (ushort)idx1;
                        allIndices[indexOffset + index++] = (ushort)idx3;
                        allIndices[indexOffset + index++] = (ushort)idx2;
                    }
                }
                vertexOffset += verticesPerPatch;
                indexOffset += indicesPerPatch;
            }

            return SKVertices.CreateCopy(SKVertexMode.Triangles, allVertices, null, allColors, allIndices);
        }

        /// <summary>
        /// Evaluates the tensor-product Bézier surface for a 4x4 patch at (u, v) using direct vectorized operations.
        /// </summary>
        /// <param name="u">Normalized horizontal coordinate (0..1).</param>
        /// <param name="v">Normalized vertical coordinate (0..1).</param>
        /// <param name="controlPoints">Array of 16 control points.</param>
        /// <returns>Surface position as SKPoint.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SKPoint EvalTensorBezier(float u, float v, SKPoint[] controlPoints)
        {
            if (controlPoints == null || controlPoints.Length != 16)
            {
                throw new System.ArgumentException("controlPoints must have 16 elements.");
            }

            Vector4 bu = ComputeVectorBezierCoefficients(u);
            Vector4 bv = ComputeVectorBezierCoefficients(v);

            // Load control points in spiral order for each row
            Vector4 x0 = new Vector4(
                controlPoints[P00].X,
                controlPoints[P10].X,
                controlPoints[P20].X,
                controlPoints[P30].X);
            Vector4 x1 = new Vector4(
                controlPoints[P01].X,
                controlPoints[P11].X,
                controlPoints[P21].X,
                controlPoints[P31].X);
            Vector4 x2 = new Vector4(
                controlPoints[P02].X,
                controlPoints[P12].X,
                controlPoints[P22].X,
                controlPoints[P32].X);
            Vector4 x3 = new Vector4(
                controlPoints[P03].X,
                controlPoints[P13].X,
                controlPoints[P23].X,
                controlPoints[P33].X);

            Vector4 y0 = new Vector4(
                controlPoints[P00].Y,
                controlPoints[P10].Y,
                controlPoints[P20].Y,
                controlPoints[P30].Y);
            Vector4 y1 = new Vector4(
                controlPoints[P01].Y,
                controlPoints[P11].Y,
                controlPoints[P21].Y,
                controlPoints[P31].Y);
            Vector4 y2 = new Vector4(
                controlPoints[P02].Y,
                controlPoints[P12].Y,
                controlPoints[P22].Y,
                controlPoints[P32].Y);
            Vector4 y3 = new Vector4(
                controlPoints[P03].Y,
                controlPoints[P13].Y,
                controlPoints[P23].Y,
                controlPoints[P33].Y);

            Vector4 dx = new Vector4(
                Vector4.Dot(x0, bu),
                Vector4.Dot(x1, bu),
                Vector4.Dot(x2, bu),
                Vector4.Dot(x3, bu));

            Vector4 dy = new Vector4(
                Vector4.Dot(y0, bu),
                Vector4.Dot(y1, bu),
                Vector4.Dot(y2, bu),
                Vector4.Dot(y3, bu));

            float x = Vector4.Dot(dx, bv);
            float y = Vector4.Dot(dy, bv);

            return new SKPoint(x, y);
        }

        /// <summary>
        /// Bilinearly interpolates the four corner colors for a patch at (u, v) using vectorized operations.
        /// </summary>
        /// <param name="u">Normalized horizontal coordinate (0..1).</param>
        /// <param name="v">Normalized vertical coordinate (0..1).</param>
        /// <param name="cornerColors">Array of 4 SKColor values (order: bottom-left, top-left, top-right, bottom-right).</param>
        /// <returns>Interpolated SKColor.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SKColor InterpolateCornerColors(float u, float v, SKColor[] cornerColors)
        {
            // Compute bilinear weights for each corner
            float bottomLeftWeight = (1.0f - u) * (1.0f - v);
            float topLeftWeight = (1.0f - u) * v;
            float topRightWeight = u * v;
            float bottomRightWeight = u * (1.0f - v);

            var weights = new Vector4(bottomLeftWeight, topLeftWeight, topRightWeight, bottomRightWeight);

            // Vectorize RGBA channels for the four corners
            var redChannel = new Vector4(
                cornerColors[0].Red,
                cornerColors[1].Red,
                cornerColors[2].Red,
                cornerColors[3].Red);
            var greenChannel = new Vector4(
                cornerColors[0].Green,
                cornerColors[1].Green,
                cornerColors[2].Green,
                cornerColors[3].Green);
            var blueChannel = new Vector4(
                cornerColors[0].Blue,
                cornerColors[1].Blue,
                cornerColors[2].Blue,
                cornerColors[3].Blue);
            var alphaChannel = new Vector4(
                cornerColors[0].Alpha,
                cornerColors[1].Alpha,
                cornerColors[2].Alpha,
                cornerColors[3].Alpha);

            float interpolatedRed = Vector4.Dot(redChannel, weights);
            float interpolatedGreen = Vector4.Dot(greenChannel, weights);
            float interpolatedBlue = Vector4.Dot(blueChannel, weights);
            float interpolatedAlpha = Vector4.Dot(alphaChannel, weights);

            return new SKColor(
                ClampToByte(interpolatedRed),
                ClampToByte(interpolatedGreen),
                ClampToByte(interpolatedBlue),
                ClampToByte(interpolatedAlpha));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ClampToByte(float value)
        {
            if (value < 0f) return 0;
            if (value > 255f) return 255;
            return (byte)value;
        }

        /// <summary>
        /// Precomputes the cubic Bézier coefficients for a given parameter as a Vector4.
        /// </summary>
        /// <param name="t">Normalized parameter (0..1).</param>
        /// <returns>Vector4 of 4 coefficients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 ComputeVectorBezierCoefficients(float t)
        {
            float oneMinusT = 1.0f - t;
            return new Vector4(
                oneMinusT * oneMinusT * oneMinusT,
                3.0f * t * oneMinusT * oneMinusT,
                3.0f * t * t * oneMinusT,
                t * t * t);
        }
    }
}
