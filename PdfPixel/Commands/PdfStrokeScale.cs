using PdfPixel.Geometry;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// <para>
/// How much a stroke must be widened, per axis, to cover at least one device pixel on both of them, so
/// that thin lines stay visible when zoomed out. An axis the stroke already covers keeps a factor of
/// one, so the widening stops once the line is wide enough to stand on its own.
/// </para>
/// <para>
/// The two factors are equal under a matrix that scales both axes alike, and a single stroke width then
/// expresses the whole pen. They differ under an anisotropic matrix, where the pen is an ellipse that no
/// single stroke width can describe, and a rendering engine has to reach for its own way of drawing one.
/// </para>
/// </summary>
public readonly struct PdfStrokeScale : IEquatable<PdfStrokeScale>
{
    // Axes that ask for nearly the same widening are treated as one: the difference is invisible, and an
    // elliptical pen costs far more to draw than a round one.
    private const float UniformityTolerance = 0.01f;

    private PdfStrokeScale(float x, float y, float penWidth)
    {
        X = x;
        Y = y;
        PenWidth = penWidth;
    }

    /// <summary>
    /// Widening factor along the x axis.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Widening factor along the y axis.
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Width of the pen before widening: the authored line width, or one for a hairline, whose whole
    /// width comes from the factors.
    /// </summary>
    public float PenWidth { get; }

    /// <summary>
    /// True when both axes widen alike, so <see cref="UniformWidth"/> describes the pen completely.
    /// </summary>
    public bool IsUniform => X == Y;

    /// <summary>
    /// The width to stroke with when <see cref="IsUniform"/>. Meaningless otherwise, because the pen is
    /// an ellipse rather than a circle.
    /// </summary>
    public float UniformWidth => PenWidth * X;

    /// <summary>
    /// Computes the widening a stroke of <paramref name="lineWidth"/> needs under the device matrix of
    /// <paramref name="executionContext"/>. A line width of zero or less is the PDF hairline, which comes
    /// out as exactly one device pixel on both axes.
    /// </summary>
    public static PdfStrokeScale Create(PdfCommandExecutionContext executionContext, float lineWidth)
    {
        if (executionContext == null)
        {
            throw new ArgumentNullException(nameof(executionContext));
        }

        PdfMatrix matrix = CommandHelpers.GetScaledMatrix(executionContext);
        PdfPoint origin = matrix.MapPoint(PdfPoint.Empty);
        PdfPoint mappedX = matrix.MapPoint(new PdfPoint(1, 0));
        PdfPoint mappedY = matrix.MapPoint(new PdfPoint(0, 1));

        float axisXx = mappedX.X - origin.X;
        float axisXy = mappedX.Y - origin.Y;
        float axisYx = mappedY.X - origin.X;
        float axisYy = mappedY.Y - origin.Y;

        float normX = MathF.Sqrt((axisXx * axisXx) + (axisXy * axisXy));
        float normY = MathF.Sqrt((axisYx * axisYx) + (axisYy * axisYy));
        float penWidth = (lineWidth > 0) ? lineWidth : 1f;

        if (normX <= 0 || normY <= 0)
        {
            return new PdfStrokeScale(1f, 1f, penWidth);
        }

        if (lineWidth <= 0)
        {
            return Create(1f / normX, 1f / normY, penWidth);
        }

        float deviceWidthX = normX * lineWidth;
        float deviceWidthY = normY * lineWidth;

        return Create(
            (deviceWidthX < 1f) ? 1f / deviceWidthX : 1f,
            (deviceWidthY < 1f) ? 1f / deviceWidthY : 1f,
            penWidth);
    }

    /// <inheritdoc />
    public bool Equals(PdfStrokeScale other) => X == other.X && Y == other.Y && PenWidth == other.PenWidth;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PdfStrokeScale other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y, PenWidth);

    /// <summary>
    /// Returns whether two stroke scales are equal.
    /// </summary>
    public static bool operator ==(in PdfStrokeScale left, in PdfStrokeScale right) => left.Equals(right);

    /// <summary>
    /// Returns whether two stroke scales differ.
    /// </summary>
    public static bool operator !=(in PdfStrokeScale left, in PdfStrokeScale right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"[{X:0.###} {Y:0.###}] x {PenWidth:0.###}";

    private static PdfStrokeScale Create(float x, float y, float penWidth)
    {
        float larger = Math.Max(x, y);

        return (Math.Abs(x - y) <= UniformityTolerance * larger)
            ? new PdfStrokeScale(larger, larger, penWidth)
            : new PdfStrokeScale(x, y, penWidth);
    }
}
