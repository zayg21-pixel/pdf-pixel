using PdfReader.Color.Transform;
using PdfReader.Shading.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfReader.Shading.Builder;

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
    private const int P21 = 15;  // (2,1)
    private const int P31 = 8;   // (3,1)
    private const int P02 = 2;   // (0,2)
    private const int P12 = 13;  // (1,2)
    private const int P22 = 14;  // (2,2)
    private const int P32 = 7;   // (3,2)
    private const int P03 = 3;   // (0,3)
    private const int P13 = 4;   // (1,3)
    private const int P23 = 5;   // (2,3)
    private const int P33 = 6;   // (3,3)

    private static readonly int[] ControlPointIndexColumnMap =
    [
        P00, P01, P02, P03,
        P10, P11, P12, P13,
        P20, P21, P22, P23,
        P30, P31, P32, P33
    ];

    private static readonly int[] BoundaryControlPointColumnMap =
    [
        P00, P03, P00, P30,
        P10, P13, P01, P31,
        P20, P23, P02, P32,
        P30, P33, P03, P33
    ];

    /// <summary>
    /// Creates tessellated vertices for all Type 6/7 mesh patches in a single batch for efficient rendering.
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

        // Adjust tessellation to avoid 16-bit index overflow in SKVertices.
        // totalVertices = patches.Count * (tessellation + 1)^2 must be <= 65535.
        int maxVertices = ushort.MaxValue;
        int safeVertexCountPerPatch = (int)MathF.Floor(MathF.Sqrt(maxVertices / (float)patches.Count));
        tessellation = Math.Max(1, Math.Min(tessellation, safeVertexCountPerPatch - 1));

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
                    SKPoint evaluatedPoing;
                    SKPoint[] patchPoints = patch.Points;
                    if (patchPoints.Length == 16)
                    {
                        evaluatedPoing = EvalTensorBezier(u, v, patchPoints);
                    }
                    else if (patchPoints.Length == 12)
                    {
                        evaluatedPoing = EvalCoons(u, v, patchPoints);
                    }
                    else
                    {
                        throw new ArgumentException("Unsupported control point count for mesh patch. Expected 12 or 16.");
                    }

                    allVertices[vertexOffset + vertexIndex] = evaluatedPoing;
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
            throw new ArgumentException("controlPoints must have 16 elements.");
        }

        Vector4 bu = ComputeVectorBezierCoefficients(u);
        Vector4 bv = ComputeVectorBezierCoefficients(v);

        // Build X and Y coefficient matrices directly using Unsafe to write into MatrixStruct storage
        MatrixStruct mx = default;
        MatrixStruct my = default;
        ref float mxRef = ref Unsafe.As<MatrixStruct, float>(ref mx);
        ref float myRef = ref Unsafe.As<MatrixStruct, float>(ref my);

        for (int matrixIndex = 0; matrixIndex < 16; matrixIndex++)
        {
            int controlPointIndex = ControlPointIndexColumnMap[matrixIndex];
            SKPoint p = controlPoints[controlPointIndex];
            Unsafe.Add(ref mxRef, matrixIndex) = p.X;
            Unsafe.Add(ref myRef, matrixIndex) = p.Y;
        }

        Vector4 vx = new Vector4(bu.X);
        Vector4 vy = new Vector4(bu.Y);
        Vector4 vz = new Vector4(bu.Z);
        Vector4 vw = new Vector4(bu.W);

        // Evaluate bu^T * M * bv using vectorized transform
        Vector4 dx = mx.Row0 * vx + mx.Row1 * vy + mx.Row2 * vz + mx.Row3 * vw;
        Vector4 dy = my.Row0 * vx + my.Row1 * vy + my.Row2 * vz + my.Row3 * vw;

        float x = ColorVectorUtilities.CustomDot(dx, bv);
        float y = ColorVectorUtilities.CustomDot(dy, bv);

        return new SKPoint(x, y);
    }

    /// <summary>
    /// Evaluates a Coons patch (Type 6) at (u, v) using 12 boundary control points without central points.
    /// Order of ALL points matches Type 7 spiral order; Type 6 omits the last 4 central points.
    /// </summary>
    /// <param name="u">Normalized horizontal coordinate (0..1).</param>
    /// <param name="v">Normalized vertical coordinate (0..1).</param>
    /// <param name="controlPoints">Array of 12 control points.</param>
    /// <returns>Surface position as SKPoint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKPoint EvalCoons(float u, float v, SKPoint[] controlPoints)
    {
        if (controlPoints == null || controlPoints.Length != 12)
        {
            throw new ArgumentException("controlPoints must have 12 elements for Coons patch.");
        }

        // Precompute Bézier coefficients once
        Vector4 bu = ComputeVectorBezierCoefficients(u);
        Vector4 bv = ComputeVectorBezierCoefficients(v);

        // Build boundary curve matrices column-wise in a single loop
        MatrixStruct boundaryX = default;
        MatrixStruct boundaryY = default;
        ref float bxRef = ref Unsafe.As<MatrixStruct, float>(ref boundaryX);
        ref float byRef = ref Unsafe.As<MatrixStruct, float>(ref boundaryY);

        for (int matrixIndex = 0; matrixIndex < 16; matrixIndex++)
        {
            int cpIndex = BoundaryControlPointColumnMap[matrixIndex];
            SKPoint p = controlPoints[cpIndex];
            Unsafe.Add(ref bxRef, matrixIndex) = p.X;
            Unsafe.Add(ref byRef, matrixIndex) = p.Y;
        }

        // Evaluate u-parametric edges (bottom/top) and v-parametric edges (left/right) via column-wise accumulations
        Vector4 buX = new Vector4(bu.X);
        Vector4 buY = new Vector4(bu.Y);
        Vector4 buZ = new Vector4(bu.Z);
        Vector4 buW = new Vector4(bu.W);

        Vector4 bvX = new Vector4(bv.X);
        Vector4 bvY = new Vector4(bv.Y);
        Vector4 bvZ = new Vector4(bv.Z);
        Vector4 bvW = new Vector4(bv.W);

        // Rows: Row0=Bottom(u), Row1=Top(u), Row2=Left(v), Row3=Right(v)
        Vector4 uX = boundaryX.Row0 * buX + boundaryX.Row1 * buY + boundaryX.Row2 * buZ + boundaryX.Row3 * buW;
        Vector4 uY = boundaryY.Row0 * buX + boundaryY.Row1 * buY + boundaryY.Row2 * buZ + boundaryY.Row3 * buW;

        Vector4 vX = boundaryX.Row0 * bvX + boundaryX.Row1 * bvY + boundaryX.Row2 * bvZ + boundaryX.Row3 * bvW;
        Vector4 vY = boundaryY.Row0 * bvX + boundaryY.Row1 * bvY + boundaryY.Row2 * bvZ + boundaryY.Row3 * bvW;

        float b0X = uX.X;
        float b1X = uX.Y;
        float b0Y = uY.X;
        float b1Y = uY.Y;

        float l0X = vX.Z;
        float l1X = vX.W;
        float l0Y = vY.Z;
        float l1Y = vY.W;

        // Vectorized corner bilinear interpolation: 2 dot products
        float oneMinusU = 1.0f - u;
        float oneMinusV = 1.0f - v;
        Vector4 bilinearWeights = new Vector4(
            oneMinusU * oneMinusV,
            u * oneMinusV,
            oneMinusU * v,
            u * v);

        Vector4 cornerX = new Vector4(
            controlPoints[P00].X, controlPoints[P30].X,
            controlPoints[P03].X, controlPoints[P33].X);
        Vector4 cornerY = new Vector4(
            controlPoints[P00].Y, controlPoints[P30].Y,
            controlPoints[P03].Y, controlPoints[P33].Y);

        float bilinearX = ColorVectorUtilities.CustomDot(cornerX, bilinearWeights);
        float bilinearY = ColorVectorUtilities.CustomDot(cornerY, bilinearWeights);

        // Final Coons blending: (1-v)*b0 + v*b1 + (1-u)*l0 + u*l1 - bilinear
        float finalX = (oneMinusV * b0X + v * b1X) + (oneMinusU * l0X + u * l1X) - bilinearX;
        float finalY = (oneMinusV * b0Y + v * b1Y) + (oneMinusU * l0Y + u * l1Y) - bilinearY;

        return new SKPoint(finalX, finalY);
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
        var oneMinusU = 1.0f - u;
        var oneMinusV = 1.0f - v;
        float bottomLeftWeight = oneMinusU * oneMinusV;
        float topLeftWeight = oneMinusU * v;
        float topRightWeight = u * v;
        float bottomRightWeight = u * oneMinusV;

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

        float interpolatedRed = ColorVectorUtilities.CustomDot(redChannel, weights);
        float interpolatedGreen = ColorVectorUtilities.CustomDot(greenChannel, weights);
        float interpolatedBlue = ColorVectorUtilities.CustomDot(blueChannel, weights);
        float interpolatedAlpha = ColorVectorUtilities.CustomDot(alphaChannel, weights);

        return new SKColor(
            ClampToByte(interpolatedRed),
            ClampToByte(interpolatedGreen),
            ClampToByte(interpolatedBlue),
            ClampToByte(interpolatedAlpha));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampToByte(float value)
    {
        if (value < 0f)
        {
            return 0;
        }
        if (value > 255f)
        {
            return 255;
        }
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

    [StructLayout(LayoutKind.Explicit)]
    private struct MatrixStruct
    {
        [FieldOffset(0)]
        public Vector4 Row0;

        [FieldOffset(16)]
        public Vector4 Row1;

        [FieldOffset(32)]
        public Vector4 Row2;

        [FieldOffset(48)]
        public Vector4 Row3;
    }
}
