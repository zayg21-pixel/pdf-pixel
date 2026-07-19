using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PdfPixel.Geometry;

/// <summary>
/// A 2D affine transformation matrix with 6 operands, structurally compatible with the
/// first 6 fields of Skia's <c>SKMatrix</c> (its 3 perspective fields are not needed for PDF).
/// </summary>
public readonly struct PdfMatrix
{
    /// <summary>
    /// Initializes a new <see cref="PdfMatrix"/> from its 6 operands.
    /// </summary>
    public PdfMatrix(float scaleX, float skewX, float transX, float skewY, float scaleY, float transY)
    {
        ScaleX = scaleX;
        SkewX = skewX;
        TransX = transX;
        SkewY = skewY;
        ScaleY = scaleY;
        TransY = transY;
    }

    /// <summary>
    /// Horizontal scale.
    /// </summary>
    public float ScaleX { get; }

    /// <summary>
    /// Horizontal skew.
    /// </summary>
    public float SkewX { get; }

    /// <summary>
    /// Horizontal translation.
    /// </summary>
    public float TransX { get; }

    /// <summary>
    /// Vertical skew.
    /// </summary>
    public float SkewY { get; }

    /// <summary>
    /// Vertical scale.
    /// </summary>
    public float ScaleY { get; }

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
    public bool IsIdentity => Equals(Identity);

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
    /// Returns null if the operand list has insufficient elements.
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

        float a = operands[0].AsFloat();
        float b = operands[1].AsFloat();
        float c = operands[2].AsFloat();
        float d = operands[3].AsFloat();
        float e = operands[4].AsFloat();
        float f = operands[5].AsFloat();

        return new PdfMatrix(a, c, e, b, d, f);
    }

    /// <summary>
    /// Converts this matrix to an <see cref="SKMatrix"/>.
    /// </summary>
    internal SKMatrix ToSkMatrix() => new(ScaleX, SkewX, TransX, SkewY, ScaleY, TransY, 0, 0, 1);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{ScaleX.ToString(CultureInfo.InvariantCulture)} {SkewY.ToString(CultureInfo.InvariantCulture)} {SkewX.ToString(CultureInfo.InvariantCulture)} {ScaleY.ToString(CultureInfo.InvariantCulture)} {TransX.ToString(CultureInfo.InvariantCulture)} {TransY.ToString(CultureInfo.InvariantCulture)}]";
}
