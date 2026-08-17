using PdfPixel.Color;
using PdfPixel.Color.Transform;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Shading.Decoding;
using PdfPixel.Shading.Model;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Shading.Builder;

/// <summary>
/// Provides static methods for evaluating tensor-product Bézier surfaces and interpolating corner colors for mesh patches.
/// </summary>
internal static class MeshEvaluator
{
    // One interpolation weight per lane of a Vector4.
    private const int MaxInterpolatedVertices = 4;

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
    /// Creates vertices for all Type 4/5 Gouraud triangles in a single batch for efficient rendering.
    /// </summary>
    /// <remarks>
    /// A triangle whose vertices carry a parametric value is subdivided barycentrically, so that the
    /// parametric value is what gets interpolated and the shading's function is evaluated for every
    /// interpolated value. Triangles carrying colors directly are emitted unchanged.
    /// </remarks>
    /// <param name="triangles">List of decoded triangles, three vertices each.</param>
    /// <param name="colorResolver">Resolver for the color of an interpolated parametric value.</param>
    /// <param name="tessellation">Number of subdivisions per triangle edge (higher = smoother).</param>
    /// <param name="observer">Execution observer for long-running operations.</param>
    /// <returns>PdfVertices instance containing all triangle vertices, colors, and indices.</returns>
    public static PdfVertices CreateVerticesForTriangles(
        List<MeshData> triangles,
        MeshColorResolver colorResolver,
        int tessellation,
        IPdfExecutionObserver? observer)
    {
        if (triangles == null || triangles.Count == 0)
        {
            throw new ArgumentException("triangles must not be null or empty.");
        }

        if (!colorResolver.IsParametric)
        {
            return CreateFlatVerticesForTriangles(triangles);
        }

        int subdivisions = LimitSubdivisions(tessellation, triangles.Count);
        int verticesPerTriangle = (subdivisions + 1) * (subdivisions + 2) / 2;
        int indicesPerTriangle = subdivisions * subdivisions * 3;

        var allVertices = new PdfPoint[verticesPerTriangle * triangles.Count];
        var allColors = new PdfColor[allVertices.Length];
        var allIndices = new ushort[indicesPerTriangle * triangles.Count];

        int vertexOffset = 0;
        int indexOffset = 0;
        for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++)
        {
            MeshData triangle = triangles[triangleIndex];
            PdfPoint[] corners = triangle.Points;
            MeshVertexColor[] cornerColors = triangle.CornerColors;

            int vertexIndex = 0;
            for (int rowIndex = 0; rowIndex <= subdivisions; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex <= rowIndex; columnIndex++)
                {
                    float weightC = columnIndex / (float)subdivisions;
                    float weightB = (rowIndex - columnIndex) / (float)subdivisions;
                    float weightA = 1f - weightB - weightC;

                    allVertices[vertexOffset + vertexIndex] = new PdfPoint(
                        (corners[0].X * weightA) + (corners[1].X * weightB) + (corners[2].X * weightC),
                        (corners[0].Y * weightA) + (corners[1].Y * weightB) + (corners[2].Y * weightC));
                    allColors[vertexOffset + vertexIndex] = colorResolver.Resolve(
                        InterpolateVertexColors(weightA, weightB, weightC, cornerColors));
                    vertexIndex++;
                }

                observer?.Notify();
            }

            int index = 0;
            for (int rowIndex = 1; rowIndex <= subdivisions; rowIndex++)
            {
                int upperRowStart = vertexOffset + (rowIndex * (rowIndex - 1) / 2);
                int lowerRowStart = upperRowStart + rowIndex;

                for (int columnIndex = 0; columnIndex < rowIndex; columnIndex++)
                {
                    allIndices[indexOffset + index++] = (ushort)(upperRowStart + columnIndex);
                    allIndices[indexOffset + index++] = (ushort)(lowerRowStart + columnIndex);
                    allIndices[indexOffset + index++] = (ushort)(lowerRowStart + columnIndex + 1);

                    if (columnIndex < (rowIndex - 1))
                    {
                        allIndices[indexOffset + index++] = (ushort)(upperRowStart + columnIndex);
                        allIndices[indexOffset + index++] = (ushort)(lowerRowStart + columnIndex + 1);
                        allIndices[indexOffset + index++] = (ushort)(upperRowStart + columnIndex + 1);
                    }
                }
            }

            vertexOffset += verticesPerTriangle;
            indexOffset += indicesPerTriangle;
        }

        return new PdfVertices(allVertices, allColors, allIndices);
    }

    /// <summary>
    /// Creates tessellated vertices for all Type 6/7 mesh patches in a single batch for efficient rendering.
    /// </summary>
    /// <param name="patches">List of mesh patches to tessellate.</param>
    /// <param name="colorResolver">Resolver for the color of an interpolated parametric value.</param>
    /// <param name="tessellation">Number of subdivisions per axis (higher = smoother).</param>
    /// <param name="observer">Execution observer for long-running operations.</param>
    /// <returns>PdfVertices instance containing all tessellated mesh vertices, colors, and indices.</returns>
    public static PdfVertices CreateVerticesForPatches(List<MeshData> patches, MeshColorResolver colorResolver, int tessellation, IPdfExecutionObserver? observer)
    {
        if (patches == null || patches.Count == 0)
        {
            throw new ArgumentException("patches must not be null or empty.");
        }

        if (tessellation < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(tessellation), "Tessellation must be >= 1.");
        }

        // Adjust tessellation to avoid 16-bit index overflow.
        // totalVertices = patches.Count * (tessellation + 1)^2 must be <= 65535.
        const int maxVertices = ushort.MaxValue;
        var safeVertexCountPerPatch = (int)MathF.Floor(MathF.Sqrt(maxVertices / (float)patches.Count));
        tessellation = Math.Max(1, Math.Min(tessellation, safeVertexCountPerPatch - 1));

        int vertexCountPerAxis = tessellation + 1;
        int verticesPerPatch = vertexCountPerAxis * vertexCountPerAxis;
        int quadsPerPatch = tessellation * tessellation;
        int indicesPerPatch = quadsPerPatch * 6;

        int totalVertices = verticesPerPatch * patches.Count;
        int totalIndices = indicesPerPatch * patches.Count;

        var allVertices = new PdfPoint[totalVertices];
        var allColors = new PdfColor[totalVertices];
        var allIndices = new ushort[totalIndices];

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
                    PdfPoint evaluatedPoint;
                    PdfPoint[] patchPoints = patch.Points;
                    if (patchPoints.Length == 16)
                    {
                        evaluatedPoint = EvalTensorBezier(u, v, patchPoints);
                    }
                    else if (patchPoints.Length == 12)
                    {
                        evaluatedPoint = EvalCoons(u, v, patchPoints);
                    }
                    else
                    {
                        throw new ArgumentException("Unsupported control point count for mesh patch. Expected 12 or 16.");
                    }

                    allVertices[vertexOffset + vertexIndex] = evaluatedPoint;
                    allColors[vertexOffset + vertexIndex] = colorResolver.Resolve(InterpolateCornerColors(u, v, patch.CornerColors));
                    vertexIndex++;
                }

                observer?.Notify();
            }

            int index = 0;
            for (int rowIndex = 0; rowIndex < tessellation; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < tessellation; columnIndex++)
                {
                    int idx0 = vertexOffset + (rowIndex * vertexCountPerAxis) + columnIndex;
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

        return new PdfVertices(allVertices, allColors, allIndices);
    }

    /// <summary>
    /// Evaluates the tensor-product Bézier surface for a 4x4 patch at (u, v) using direct vectorized operations.
    /// </summary>
    /// <param name="u">Normalized horizontal coordinate (0..1).</param>
    /// <param name="v">Normalized vertical coordinate (0..1).</param>
    /// <param name="controlPoints">Array of 16 control points.</param>
    /// <returns>Surface position as PdfPoint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfPoint EvalTensorBezier(float u, float v, PdfPoint[] controlPoints)
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
            PdfPoint p = controlPoints[controlPointIndex];
            Unsafe.Add(ref mxRef, matrixIndex) = p.X;
            Unsafe.Add(ref myRef, matrixIndex) = p.Y;
        }

        Vector4 vx = new(bu.X);
        Vector4 vy = new(bu.Y);
        Vector4 vz = new(bu.Z);
        Vector4 vw = new(bu.W);

        // Evaluate bu^T * M * bv using vectorized transform
        Vector4 dx = (mx.Row0 * vx) + (mx.Row1 * vy) + (mx.Row2 * vz) + (mx.Row3 * vw);
        Vector4 dy = (my.Row0 * vx) + (my.Row1 * vy) + (my.Row2 * vz) + (my.Row3 * vw);

        float x = ColorVectorUtilities.CustomDot(dx, bv);
        float y = ColorVectorUtilities.CustomDot(dy, bv);

        return new PdfPoint(x, y);
    }

    /// <summary>
    /// Evaluates a Coons patch (Type 6) at (u, v) using 12 boundary control points without central points.
    /// Order of ALL points matches Type 7 spiral order; Type 6 omits the last 4 central points.
    /// </summary>
    /// <param name="u">Normalized horizontal coordinate (0..1).</param>
    /// <param name="v">Normalized vertical coordinate (0..1).</param>
    /// <param name="controlPoints">Array of 12 control points.</param>
    /// <returns>Surface position as PdfPoint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfPoint EvalCoons(float u, float v, PdfPoint[] controlPoints)
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
            PdfPoint p = controlPoints[cpIndex];
            Unsafe.Add(ref bxRef, matrixIndex) = p.X;
            Unsafe.Add(ref byRef, matrixIndex) = p.Y;
        }

        // Evaluate u-parametric edges (bottom/top) and v-parametric edges (left/right) via column-wise accumulations
        Vector4 buX = new(bu.X);
        Vector4 buY = new(bu.Y);
        Vector4 buZ = new(bu.Z);
        Vector4 buW = new(bu.W);

        Vector4 bvX = new(bv.X);
        Vector4 bvY = new(bv.Y);
        Vector4 bvZ = new(bv.Z);
        Vector4 bvW = new(bv.W);

        // Rows: Row0=Bottom(u), Row1=Top(u), Row2=Left(v), Row3=Right(v)
        Vector4 uX = (boundaryX.Row0 * buX) + (boundaryX.Row1 * buY) + (boundaryX.Row2 * buZ) + (boundaryX.Row3 * buW);
        Vector4 uY = (boundaryY.Row0 * buX) + (boundaryY.Row1 * buY) + (boundaryY.Row2 * buZ) + (boundaryY.Row3 * buW);

        Vector4 vX = (boundaryX.Row0 * bvX) + (boundaryX.Row1 * bvY) + (boundaryX.Row2 * bvZ) + (boundaryX.Row3 * bvW);
        Vector4 vY = (boundaryY.Row0 * bvX) + (boundaryY.Row1 * bvY) + (boundaryY.Row2 * bvZ) + (boundaryY.Row3 * bvW);

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
        Vector4 bilinearWeights = new(
            oneMinusU * oneMinusV,
            u * oneMinusV,
            oneMinusU * v,
            u * v);

        Vector4 cornerX = new(
            controlPoints[P00].X,
            controlPoints[P30].X,
            controlPoints[P03].X,
            controlPoints[P33].X);
        Vector4 cornerY = new(
            controlPoints[P00].Y,
            controlPoints[P30].Y,
            controlPoints[P03].Y,
            controlPoints[P33].Y);

        float bilinearX = ColorVectorUtilities.CustomDot(cornerX, bilinearWeights);
        float bilinearY = ColorVectorUtilities.CustomDot(cornerY, bilinearWeights);

        // Final Coons blending: (1-v)*b0 + v*b1 + (1-u)*l0 + u*l1 - bilinear
        float finalX = (oneMinusV * b0X) + (v * b1X) + ((oneMinusU * l0X) + (u * l1X)) - bilinearX;
        float finalY = (oneMinusV * b0Y) + (v * b1Y) + ((oneMinusU * l0Y) + (u * l1Y)) - bilinearY;

        return new PdfPoint(finalX, finalY);
    }

    /// <summary>
    /// Copies the triangles' own vertices and colors into a single batch, without subdivision.
    /// </summary>
    private static PdfVertices CreateFlatVerticesForTriangles(List<MeshData> triangles)
    {
        var allPoints = new PdfPoint[triangles.Count * 3];
        var allColors = new PdfColor[allPoints.Length];

        for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++)
        {
            MeshData triangle = triangles[triangleIndex];
            int offset = triangleIndex * 3;

            Array.Copy(triangle.Points, 0, allPoints, offset, 3);
            for (int cornerIndex = 0; cornerIndex < 3; cornerIndex++)
            {
                allColors[offset + cornerIndex] = triangle.CornerColors[cornerIndex].Color;
            }
        }

        return new PdfVertices(allPoints, allColors, null);
    }

    /// <summary>
    /// Reduces the requested subdivision count until the barycentric grids of all triangles fit
    /// into the 16-bit index space.
    /// </summary>
    private static int LimitSubdivisions(int tessellation, int triangleCount)
    {
        int vertexBudget = (ushort.MaxValue + 1) / triangleCount;
        var safeSubdivisions = (int)MathF.Floor((MathF.Sqrt((8f * vertexBudget) + 1f) - 3f) / 2f);
        return Math.Max(1, Math.Min(tessellation, safeSubdivisions));
    }

    /// <summary>
    /// Bilinearly interpolates the four corners' color data for a patch at (u, v).
    /// </summary>
    /// <param name="u">Normalized horizontal coordinate (0..1).</param>
    /// <param name="v">Normalized vertical coordinate (0..1).</param>
    /// <param name="cornerColors">Array of 4 corner values (order: bottom-left, top-left, top-right, bottom-right).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MeshVertexColor InterpolateCornerColors(float u, float v, MeshVertexColor[] cornerColors)
    {
        float oneMinusU = 1.0f - u;
        float oneMinusV = 1.0f - v;
        Vector4 weights = new(oneMinusU * oneMinusV, oneMinusU * v, u * v, u * oneMinusV);

        return InterpolateVertexColors(weights, cornerColors);
    }

    /// <summary>
    /// Interpolates a triangle's three vertices' color data at the given barycentric weights.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MeshVertexColor InterpolateVertexColors(float weightA, float weightB, float weightC, MeshVertexColor[] vertexColors)
        => InterpolateVertexColors(new Vector4(weightA, weightB, weightC, 0f), vertexColors);

    /// <summary>
    /// Interpolates the color data of up to <see cref="MaxInterpolatedVertices"/> vertices,
    /// one weight per vertex. Lanes without a vertex keep a zero weight.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MeshVertexColor InterpolateVertexColors(Vector4 weights, MeshVertexColor[] vertexColors)
    {
        if (vertexColors.Length > MaxInterpolatedVertices)
        {
            throw new ArgumentException($"At most {MaxInterpolatedVertices} vertices can be interpolated at once.");
        }

        Vector4 red = default;
        Vector4 green = default;
        Vector4 blue = default;
        Vector4 parameter = default;
        ref float redRef = ref Unsafe.As<Vector4, float>(ref red);
        ref float greenRef = ref Unsafe.As<Vector4, float>(ref green);
        ref float blueRef = ref Unsafe.As<Vector4, float>(ref blue);
        ref float parameterRef = ref Unsafe.As<Vector4, float>(ref parameter);

        for (int vertexIndex = 0; vertexIndex < vertexColors.Length; vertexIndex++)
        {
            MeshVertexColor vertexColor = vertexColors[vertexIndex];
            Unsafe.Add(ref redRef, vertexIndex) = vertexColor.Color.Red;
            Unsafe.Add(ref greenRef, vertexIndex) = vertexColor.Color.Green;
            Unsafe.Add(ref blueRef, vertexIndex) = vertexColor.Color.Blue;
            Unsafe.Add(ref parameterRef, vertexIndex) = vertexColor.Parameter;
        }

        PdfColor interpolatedColor = new(
            ColorVectorUtilities.CustomDot(red, weights),
            ColorVectorUtilities.CustomDot(green, weights),
            ColorVectorUtilities.CustomDot(blue, weights));

        return new MeshVertexColor(interpolatedColor, ColorVectorUtilities.CustomDot(parameter, weights));
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
