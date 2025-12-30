using PdfReader.Color.Icc.Model;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Transform;

/// <summary>
/// Implements <see cref="IColorTransform"/> using a 4x4 matrix for color space transformations.
/// Supports initialization from various matrix and color component representations.
/// </summary>
internal sealed class MatrixColorTransform : IColorTransform
{
    private readonly Matrix4x4 _matrix;
    private readonly Vector4 _col1;
    private readonly Vector4 _col2;
    private readonly Vector4 _col3;
    private readonly Vector4 _col4;
    private readonly bool _isIdentity;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixColorTransform"/> class with a specified 4x4 matrix.
    /// </summary>
    /// <param name="matrix">The transformation matrix.</param>
    public MatrixColorTransform(Matrix4x4 matrix)
    {
        _matrix = matrix;
        _isIdentity = _matrix.IsIdentity;
        (_col1, _col2, _col3, _col4) = DecomposeColumns(_matrix);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixColorTransform"/> class from a 3x3 matrix, optional offset, and transpose flag.
    /// </summary>
    /// <param name="matrix3x3">A 3x3 matrix as a two-dimensional float array.</param>
    /// <param name="offset">Optional offset vector (applied to translation components).</param>
    /// <param name="transpose">Whether to transpose the matrix (default: true).</param>
    public MatrixColorTransform(float[,] matrix3x3, float[] offset = default, bool transpose = true)
    {
        var matrix4X4 = ColorVectorUtilities.ToMatrix4x4(matrix3x3);

        if (transpose)
        {
            matrix4X4 = Matrix4x4.Transpose(matrix4X4);
        }

        if (offset != null && offset.Length >= 3)
        {
            matrix4X4.M41 = offset[0];
            matrix4X4.M42 = offset[1];
            matrix4X4.M43 = offset[2];
        }

        _matrix = matrix4X4;
        _isIdentity = _matrix.IsIdentity;
        (_col1, _col2, _col3, _col4) = DecomposeColumns(_matrix);
    }

    public bool IsIdentity => _isIdentity;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixColorTransform"/> class from an array of ICC XYZ components.
    /// </summary>
    /// <param name="components">Array of ICC XYZ color components.</param>
    /// <exception cref="NotSupportedException">Thrown if more than 4 components are provided.</exception>
    public MatrixColorTransform(IccXyz[] components)
    {
        if (components.Length > 4)
        {
            throw new NotSupportedException($"Invalid number of components {components.Length} for matrix transform");
        }

        float m11, m12, m13, m14;

        if (components.Length >= 1)
        {
            m11 = components[0].X;
            m12 = components[0].Y;
            m13 = components[0].Z;
            m14 = 0;
        }
        else
        {
            m11 = 0;
            m12 = 0;
            m13 = 0;
            m14 = 1;
        }

        float m21, m22, m23, m24;

        if (components.Length >= 2)
        {
            m21 = components[1].X;
            m22 = components[1].Y;
            m23 = components[1].Z;
            m24 = 0;
        }
        else
        {
            m21 = 0;
            m22 = 0;
            m23 = 0;
            m24 = 1;
        }

        float m31, m32, m33, m34;

        if (components.Length >= 3)
        {
            m31 = components[2].X;
            m32 = components[2].Y;
            m33 = components[2].Z;
            m34 = 0;
        }
        else
        {
            m31 = 0;
            m32 = 0;
            m33 = 0;
            m34 = 1;
        }

        float m41, m42, m43, m44;

        if (components.Length >= 4)
        {
            m41 = components[3].X;
            m42 = components[3].Y;
            m43 = components[3].Z;
            m44 = 0;
        }
        else
        {
            m41 = 0;
            m42 = 0;
            m43 = 0;
            m44 = 1;
        }

        _matrix = new Matrix4x4(m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44);
        _isIdentity = _matrix.IsIdentity;
        (_col1, _col2, _col3, _col4) = DecomposeColumns(_matrix);
    }

    /// <summary>
    /// Transforms the input color vector using the transformation matrix.
    /// </summary>
    /// <param name="color">The input color vector.</param>
    /// <returns>The transformed color vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(Vector4 color)
    {
        if (_isIdentity)
        {
            return color;
        }

        // Using precomputed column vectors for efficient matrix-vector multiplication.
        Vector4 vx = new Vector4(color.X);
        Vector4 vy = new Vector4(color.Y);
        Vector4 vz = new Vector4(color.Z);
        Vector4 vw = new Vector4(color.W);

        Vector4 res = (vx * _col1) + (vy * _col2);
        res = res + (vz * _col3);
        res = res + (vw * _col4);
        return res;
    }

    /// <summary>
    /// Decomposes a Matrix4x4 into its column vectors.
    /// </summary>
    /// <param name="matrix">Source matrix.</param>
    /// <returns>Tuple of four columns (c1, c2, c3, c4).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Vector4 c1, Vector4 c2, Vector4 c3, Vector4 c4) DecomposeColumns(Matrix4x4 matrix)
    {
        Vector4 c1 = new Vector4(matrix.M11, matrix.M12, matrix.M13, matrix.M14);
        Vector4 c2 = new Vector4(matrix.M21, matrix.M22, matrix.M23, matrix.M24);
        Vector4 c3 = new Vector4(matrix.M31, matrix.M32, matrix.M33, matrix.M34);
        Vector4 c4 = new Vector4(matrix.M41, matrix.M42, matrix.M43, matrix.M44);
        return (c1, c2, c3, c4);
    }
}
