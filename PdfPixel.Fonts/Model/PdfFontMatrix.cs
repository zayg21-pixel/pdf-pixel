using System.Globalization;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// A 2D affine transformation matrix with 6 operands, in the same [a b c d e f] operand order as a
/// CFF/Type1/Type3 FontMatrix. Structurally the font-space counterpart of <c>PdfPixel.Geometry.PdfMatrix</c>
/// (that type lives in the main PdfPixel assembly, which PdfPixel.Fonts does not reference).
/// </summary>
public readonly struct PdfFontMatrix
{
    /// <summary>
    /// Initializes a new <see cref="PdfFontMatrix"/> from its 6 operands.
    /// </summary>
    public PdfFontMatrix(float scaleX, float skewX, float transX, float skewY, float scaleY, float transY)
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
    public static PdfFontMatrix Identity { get; } = new(1, 0, 0, 0, 1, 0);

    /// <summary>
    /// The CFF/Type1 spec's default FontMatrix (a uniform 1/1000 scale), used when a Top DICT or Font
    /// DICT carries no FontMatrix operator of its own.
    /// </summary>
    public static PdfFontMatrix Default { get; } = new(0.001f, 0, 0, 0, 0.001f, 0);

    /// <summary>
    /// Whether this matrix equals <see cref="Identity"/>.
    /// </summary>
    public bool IsIdentity => Equals(Identity);

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
        float scaleX = (first.ScaleX * second.ScaleX) + (first.SkewX * second.SkewY);
        float skewX = (first.ScaleX * second.SkewX) + (first.SkewX * second.ScaleY);
        float transX = (first.ScaleX * second.TransX) + (first.SkewX * second.TransY) + first.TransX;

        float skewY = (first.SkewY * second.ScaleX) + (first.ScaleY * second.SkewY);
        float scaleY = (first.SkewY * second.SkewX) + (first.ScaleY * second.ScaleY);
        float transY = (first.SkewY * second.TransX) + (first.ScaleY * second.TransY) + first.TransY;

        return new PdfFontMatrix(scaleX, skewX, transX, skewY, scaleY, transY);
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
    /// Transforms the point <c>(x, y)</c> by this matrix.
    /// </summary>
    public (float X, float Y) MapPoint(float x, float y)
    {
        if (IsIdentity)
        {
            return (x, y);
        }

        return ((ScaleX * x) + (SkewX * y) + TransX, (SkewY * x) + (ScaleY * y) + TransY);
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"[{ScaleX.ToString(CultureInfo.InvariantCulture)} {SkewX.ToString(CultureInfo.InvariantCulture)} {TransX.ToString(CultureInfo.InvariantCulture)} {SkewY.ToString(CultureInfo.InvariantCulture)} {ScaleY.ToString(CultureInfo.InvariantCulture)} {TransY.ToString(CultureInfo.InvariantCulture)}]";
}
