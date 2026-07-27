using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PdfPixel.Geometry;

/// <summary>
/// An integer-valued rectangle defined by its left, top, right, and bottom edges.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PdfIntegerRectangle : IEquatable<PdfIntegerRectangle>
{
    /// <summary>
    /// Initializes a new <see cref="PdfIntegerRectangle"/> from its edges.
    /// </summary>
    public PdfIntegerRectangle(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>
    /// Left edge.
    /// </summary>
    public int Left { get; }

    /// <summary>
    /// Top edge.
    /// </summary>
    public int Top { get; }

    /// <summary>
    /// Right edge.
    /// </summary>
    public int Right { get; }

    /// <summary>
    /// Bottom edge.
    /// </summary>
    public int Bottom { get; }

    /// <summary>
    /// Distance between <see cref="Left"/> and <see cref="Right"/>.
    /// </summary>
    public int Width => Right - Left;

    /// <summary>
    /// Distance between <see cref="Top"/> and <see cref="Bottom"/>.
    /// </summary>
    public int Height => Bottom - Top;

    /// <summary>
    /// The empty rectangle, with all edges at 0.
    /// </summary>
    public static PdfIntegerRectangle Empty { get; } = new(0, 0, 0, 0);

    /// <summary>
    /// Whether this rectangle equals <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => this == Empty;

    /// <summary>
    /// Returns the overlapping region of <paramref name="a"/> and <paramref name="b"/>, or <see cref="Empty"/> when they do not overlap.
    /// </summary>
    public static PdfIntegerRectangle Intersect(in PdfIntegerRectangle a, in PdfIntegerRectangle b)
    {
        if (!IntersectsWithInclusive(a, b))
        {
            return Empty;
        }

        return new PdfIntegerRectangle(
            Math.Max(a.Left, b.Left),
            Math.Max(a.Top, b.Top),
            Math.Min(a.Right, b.Right),
            Math.Min(a.Bottom, b.Bottom));
    }

    private static bool IntersectsWithInclusive(in PdfIntegerRectangle a, in PdfIntegerRectangle b)
        => a.Left <= b.Right && a.Right >= b.Left && a.Top <= b.Bottom && a.Bottom >= b.Top;

    /// <inheritdoc/>
    public bool Equals(PdfIntegerRectangle other)
        => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfIntegerRectangle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    /// <summary>
    /// Determines whether two rectangles have the same edges.
    /// </summary>
    public static bool operator ==(in PdfIntegerRectangle left, in PdfIntegerRectangle right) => left.Equals(right);

    /// <summary>
    /// Determines whether two rectangles have different edges.
    /// </summary>
    public static bool operator !=(in PdfIntegerRectangle left, in PdfIntegerRectangle right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{Left.ToString(CultureInfo.InvariantCulture)} {Top.ToString(CultureInfo.InvariantCulture)} {Right.ToString(CultureInfo.InvariantCulture)} {Bottom.ToString(CultureInfo.InvariantCulture)}]";
}
