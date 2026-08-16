using PdfPixel.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Geometry;

/// <summary>
/// A 2D affine transformation matrix with 6 operands.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PdfMatrix : IEquatable<PdfMatrix>
{
    /// <summary>
    /// Initializes a new <see cref="PdfMatrix"/> from its 6 operands.
    /// </summary>
    public PdfMatrix(float scaleX, float skewX, float transX, float skewY, float scaleY, float transY)
    {
        ScaleX = scaleX;
        SkewY = skewY;
        SkewX = skewX;
        ScaleY = scaleY;
        TransX = transX;
        TransY = transY;
    }

    /// <summary>
    /// Horizontal scale.
    /// </summary>
    public float ScaleX { get; }

    /// <summary>
    /// Vertical skew.
    /// </summary>
    public float SkewY { get; }

    /// <summary>
    /// Horizontal skew.
    /// </summary>
    public float SkewX { get; }

    /// <summary>
    /// Vertical scale.
    /// </summary>
    public float ScaleY { get; }

    /// <summary>
    /// Horizontal translation.
    /// </summary>
    public float TransX { get; }

    /// <summary>
    /// Vertical translation.
    /// </summary>
    public float TransY { get; }

    /// <summary>
    /// The identity matrix.
    /// </summary>
    public static PdfMatrix Identity { get; } = new(1, 0, 0, 0, 1, 0);

    /// <summary>
    /// Whether this matrix equals <see cref="Identity"/>.
    /// </summary>
    public bool IsIdentity
    {
        get
        {
            return ScaleX == 1f
                && ScaleY == 1f
                && SkewX == 0f
                && SkewY == 0f
                && TransX == 0f
                && TransY == 0f;
        }
    }

    /// <summary>
    /// Whether this matrix has no skew, i.e. it only scales and translates.
    /// </summary>
    public bool IsAxisAligned => SkewX == 0 && SkewY == 0;

    /// <summary>
    /// Creates a <see cref="PdfMatrix"/> from a strongly-typed PdfArray of operands.
    /// Returns null if the array is not defined or has insufficient elements.
    /// </summary>
    public static PdfMatrix? FromArray(PdfArray? array)
    {
        if (array == null || array.Count < 6)
        {
            return null;
        }

        float a = array.GetFloatOrDefault(0);
        float b = array.GetFloatOrDefault(1);
        float c = array.GetFloatOrDefault(2);
        float d = array.GetFloatOrDefault(3);
        float e = array.GetFloatOrDefault(4);
        float f = array.GetFloatOrDefault(5);

        return new PdfMatrix(a, c, e, b, d, f);
    }

    /// <summary>
    /// Creates a <see cref="PdfMatrix"/> from PDF transformation matrix operands (legacy list form).
    /// Returns null if the operand list has insufficient elements or any operand is not numeric.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="operands"/> is null.</exception>
    public static PdfMatrix? FromOperands(List<IPdfValue> operands)
    {
        if (operands == null)
        {
            throw new ArgumentNullException(nameof(operands));
        }

        if (operands.Count < 6)
        {
            return null;
        }

        float? a = operands[0].AsFloat();
        float? b = operands[1].AsFloat();
        float? c = operands[2].AsFloat();
        float? d = operands[3].AsFloat();
        float? e = operands[4].AsFloat();
        float? f = operands[5].AsFloat();

        if (a == null
            || b == null
            || c == null
            || d == null
            || e == null
            || f == null)
        {
            return null;
        }

        return new PdfMatrix(a.Value, c.Value, e.Value, b.Value, d.Value, f.Value);
    }

    /// <summary>
    /// Creates a matrix that translates points by <paramref name="translateX"/> and <paramref name="translateY"/>.
    /// </summary>
    public static PdfMatrix CreateTranslation(float translateX, float translateY) => new(1, 0, translateX, 0, 1, translateY);

    /// <summary>
    /// Creates a matrix that scales points by <paramref name="scaleX"/> and <paramref name="scaleY"/> about the origin.
    /// </summary>
    public static PdfMatrix CreateScale(float scaleX, float scaleY) => new(scaleX, 0, 0, 0, scaleY, 0);

    /// <summary>
    /// Creates a matrix that scales points by <paramref name="scaleX"/>/<paramref name="scaleY"/> and then
    /// translates them by <paramref name="translateX"/>/<paramref name="translateY"/>.
    /// </summary>
    public static PdfMatrix CreateScaleTranslation(float scaleX, float scaleY, float translateX, float translateY) => new(scaleX, 0, translateX, 0, scaleY, translateY);

    /// <summary>
    /// Creates a matrix that rotates points about the origin by <paramref name="degrees"/>.
    /// </summary>
    public static PdfMatrix CreateRotationDegrees(float degrees)
    {
        float radians = degrees * MathF.PI / 180.0f;
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);

        return new PdfMatrix(cos, -sin, 0, sin, cos, 0);
    }

    /// <summary>
    /// Combines two matrices, equivalent to applying <paramref name="second"/> first and then <paramref name="first"/>.
    /// </summary>
    public static PdfMatrix Concat(in PdfMatrix first, in PdfMatrix second)
    {
        if (first.IsIdentity)
        {
            return second;
        }

        if (second.IsIdentity)
        {
            return first;
        }

        return AsPdfMatrix(AsMatrix3x2(second) * AsMatrix3x2(first));
    }

    /// <summary>
    /// Returns the matrix equivalent to applying <paramref name="matrix"/> first, then this matrix.
    /// </summary>
    public PdfMatrix PreConcat(in PdfMatrix matrix) => Concat(this, matrix);

    /// <summary>
    /// Returns the matrix equivalent to applying this matrix first, then <paramref name="matrix"/>.
    /// </summary>
    public PdfMatrix PostConcat(in PdfMatrix matrix) => Concat(matrix, this);

    /// <summary>
    /// Transforms <paramref name="point"/> by this matrix.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfPoint MapPoint(in PdfPoint point)
    {
        if (IsIdentity)
        {
            return point;
        }

        Vector2 mapped = Vector2.Transform(new Vector2(point.X, point.Y), AsMatrix3x2(this));

        return new PdfPoint(mapped.X, mapped.Y);
    }

    /// <summary>
    /// Transforms <paramref name="vector"/> by this matrix, translation included.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 MapVector2(Vector2 vector)
    {
        if (IsIdentity)
        {
            return vector;
        }

        return Vector2.Transform(vector, AsMatrix3x2(this));
    }

    /// <summary>
    /// Transforms <paramref name="rect"/> by this matrix, returning the axis-aligned bounding box of its
    /// transformed corners.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfRectangle MapRect(in PdfRectangle rect)
    {
        if (IsIdentity)
        {
            return rect;
        }

        PdfPoint topLeft = MapPoint(new PdfPoint(rect.Left, rect.Top));
        PdfPoint topRight = MapPoint(new PdfPoint(rect.Right, rect.Top));
        PdfPoint bottomLeft = MapPoint(new PdfPoint(rect.Left, rect.Bottom));
        PdfPoint bottomRight = MapPoint(new PdfPoint(rect.Right, rect.Bottom));

        float left = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        float top = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        float right = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        float bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

        return new PdfRectangle(left, top, right, bottom);
    }

    /// <summary>
    /// Computes the inverse of this matrix. Returns <see cref="Identity"/> when this matrix is singular
    /// (its determinant is zero) and has no inverse.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfMatrix Invert()
    {
        if (!Matrix3x2.Invert(AsMatrix3x2(this), out Matrix3x2 inverse))
        {
            return Identity;
        }

        return AsPdfMatrix(inverse);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PdfMatrix other)
    {
        return ScaleX == other.ScaleX
            && SkewX == other.SkewX
            && TransX == other.TransX
            && SkewY == other.SkewY
            && ScaleY == other.ScaleY
            && TransY == other.TransY;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is PdfMatrix other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(ScaleX, SkewX, TransX, SkewY, ScaleY, TransY);

    /// <summary>
    /// Determines whether two matrices have the same operands.
    /// </summary>
    public static bool operator ==(in PdfMatrix left, in PdfMatrix right) => left.Equals(right);

    /// <summary>
    /// Determines whether two matrices have different operands.
    /// </summary>
    public static bool operator !=(in PdfMatrix left, in PdfMatrix right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{ScaleX.ToString(CultureInfo.InvariantCulture)} {SkewY.ToString(CultureInfo.InvariantCulture)} {SkewX.ToString(CultureInfo.InvariantCulture)} {ScaleY.ToString(CultureInfo.InvariantCulture)} {TransX.ToString(CultureInfo.InvariantCulture)} {TransY.ToString(CultureInfo.InvariantCulture)}]";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Matrix3x2 AsMatrix3x2(in PdfMatrix matrix) => Unsafe.As<PdfMatrix, Matrix3x2>(ref Unsafe.AsRef(in matrix));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfMatrix AsPdfMatrix(Matrix3x2 matrix) => Unsafe.As<Matrix3x2, PdfMatrix>(ref matrix);
}
