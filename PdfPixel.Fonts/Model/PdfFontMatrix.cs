using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// A 2D affine transformation matrix with 6 operands, in the same [a b c d e f] operand order as a
/// CFF/Type1/Type3 FontMatrix.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PdfFontMatrix : IEquatable<PdfFontMatrix>
{
    /// <summary>
    /// Initializes a new <see cref="PdfFontMatrix"/> from its 6 operands.
    /// </summary>
    public PdfFontMatrix(float scaleX, float skewX, float transX, float skewY, float scaleY, float transY)
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
    public static PdfFontMatrix Identity { get; } = new(1, 0, 0, 0, 1, 0);

    /// <summary>
    /// The CFF/Type1 spec's default FontMatrix (a uniform 1/1000 scale), used when a Top DICT or Font
    /// DICT carries no FontMatrix operator of its own.
    /// </summary>
    public static PdfFontMatrix Default { get; } = new(0.001f, 0, 0, 0, 0.001f, 0);

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
    /// Design units per em square implied by this matrix's horizontal scale, i.e. the units-per-em a
    /// CFF/Type1 FontMatrix of <c>[1/unitsPerEm 0 0 1/unitsPerEm 0 0]</c> encodes.
    /// </summary>
    public float UnitsPerEm => 1f / ScaleX;

    /// <summary>
    /// Creates a <see cref="PdfFontMatrix"/> from a CFF FontMatrix operand array ([a b c d e f]).
    /// Returns null if the array is null or has fewer than 6 elements.
    /// </summary>
    public static PdfFontMatrix? FromArray(float[]? operands)
    {
        if (operands == null || operands.Length < 6)
        {
            return null;
        }

        return new PdfFontMatrix(operands[0], operands[2], operands[4], operands[1], operands[3], operands[5]);
    }

    /// <summary>
    /// Converts this matrix back to a CFF FontMatrix operand array ([a b c d e f]).
    /// </summary>
    public float[] ToArray() => [ScaleX, SkewY, SkewX, ScaleY, TransX, TransY];

    /// <summary>
    /// Combines two matrices, equivalent to applying <paramref name="second"/> first and then <paramref name="first"/>.
    /// </summary>
    public static PdfFontMatrix Concat(in PdfFontMatrix first, in PdfFontMatrix second)
    {
        if (first.IsIdentity)
        {
            return second;
        }

        if (second.IsIdentity)
        {
            return first;
        }

        return AsPdfFontMatrix(AsMatrix3x2(second) * AsMatrix3x2(first));
    }

    /// <summary>
    /// Returns the matrix equivalent to applying <paramref name="matrix"/> first, then this matrix.
    /// </summary>
    public PdfFontMatrix PreConcat(in PdfFontMatrix matrix) => Concat(this, matrix);

    /// <summary>
    /// Returns the matrix equivalent to applying this matrix first, then <paramref name="matrix"/>.
    /// </summary>
    public PdfFontMatrix PostConcat(in PdfFontMatrix matrix) => Concat(matrix, this);

    /// <summary>
    /// Transforms <paramref name="point"/> by this matrix.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfFontPoint MapPoint(in PdfFontPoint point)
    {
        if (IsIdentity)
        {
            return point;
        }

        Vector2 mapped = Vector2.Transform(new Vector2(point.X, point.Y), AsMatrix3x2(this));

        return new PdfFontPoint(mapped.X, mapped.Y);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PdfFontMatrix other)
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
    public override bool Equals(object? obj) => obj is PdfFontMatrix other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(ScaleX, SkewX, TransX, SkewY, ScaleY, TransY);

    /// <summary>
    /// Determines whether two matrices have the same operands.
    /// </summary>
    public static bool operator ==(in PdfFontMatrix left, in PdfFontMatrix right) => left.Equals(right);

    /// <summary>
    /// Determines whether two matrices have different operands.
    /// </summary>
    public static bool operator !=(in PdfFontMatrix left, in PdfFontMatrix right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{ScaleX.ToString(CultureInfo.InvariantCulture)} {SkewX.ToString(CultureInfo.InvariantCulture)} {TransX.ToString(CultureInfo.InvariantCulture)} {SkewY.ToString(CultureInfo.InvariantCulture)} {ScaleY.ToString(CultureInfo.InvariantCulture)} {TransY.ToString(CultureInfo.InvariantCulture)}]";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Matrix3x2 AsMatrix3x2(in PdfFontMatrix matrix) => Unsafe.As<PdfFontMatrix, Matrix3x2>(ref Unsafe.AsRef(in matrix));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfFontMatrix AsPdfFontMatrix(Matrix3x2 matrix) => Unsafe.As<Matrix3x2, PdfFontMatrix>(ref matrix);
}
