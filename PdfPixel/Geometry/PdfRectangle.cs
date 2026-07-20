using PdfPixel.Models;
using System;
using System.Globalization;

namespace PdfPixel.Geometry;

/// <summary>
/// A rectangle defined by its left, top, right, and bottom edges.
/// </summary>
public readonly struct PdfRectangle
{
    /// <summary>
    /// Initializes a new <see cref="PdfRectangle"/> from its edges.
    /// </summary>
    public PdfRectangle(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>
    /// Left edge.
    /// </summary>
    public float Left { get; }

    /// <summary>
    /// Top edge.
    /// </summary>
    public float Top { get; }

    /// <summary>
    /// Right edge.
    /// </summary>
    public float Right { get; }

    /// <summary>
    /// Bottom edge.
    /// </summary>
    public float Bottom { get; }

    /// <summary>
    /// Distance between <see cref="Left"/> and <see cref="Right"/>.
    /// </summary>
    public float Width => Right - Left;

    /// <summary>
    /// Distance between <see cref="Top"/> and <see cref="Bottom"/>.
    /// </summary>
    public float Height => Bottom - Top;

    /// <summary>
    /// Midpoint between <see cref="Left"/> and <see cref="Right"/>.
    /// </summary>
    public float MidX => (Left + Right) / 2f;

    /// <summary>
    /// Midpoint between <see cref="Top"/> and <see cref="Bottom"/>.
    /// </summary>
    public float MidY => (Top + Bottom) / 2f;

    /// <summary>
    /// The empty rectangle, with all edges at 0.
    /// </summary>
    public static PdfRectangle Empty { get; } = new(0, 0, 0, 0);

    /// <summary>
    /// Whether this rectangle equals <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => Equals(Empty);

    /// <summary>
    /// Creates a <see cref="PdfRectangle"/> from a PDF bounding box array.
    /// Returns null if the array is not defined or has insufficient elements.
    /// </summary>
    public static PdfRectangle? FromArray(PdfArray? array)
    {
        if (array == null || array.Count < 4)
        {
            return null;
        }

        float x0 = array.GetFloatOrDefault(0);
        float y0 = array.GetFloatOrDefault(1);
        float x1 = array.GetFloatOrDefault(2);
        float y1 = array.GetFloatOrDefault(3);

        float left = Math.Min(x0, x1);
        float top = Math.Min(y0, y1);
        float right = Math.Max(x0, x1);
        float bottom = Math.Max(y0, y1);

        return new PdfRectangle(left, top, right, bottom);
    }

    /// <summary>
    /// Returns the overlapping region of <paramref name="a"/> and <paramref name="b"/>, or <see cref="Empty"/> when they do not overlap.
    /// </summary>
    public static PdfRectangle Intersect(in PdfRectangle a, in PdfRectangle b)
    {
        if (!IntersectsWithInclusive(a, b))
        {
            return Empty;
        }

        return new PdfRectangle(
            Math.Max(a.Left, b.Left),
            Math.Max(a.Top, b.Top),
            Math.Min(a.Right, b.Right),
            Math.Min(a.Bottom, b.Bottom));
    }

    private static bool IntersectsWithInclusive(in PdfRectangle a, in PdfRectangle b)
        => a.Left <= b.Right && a.Right >= b.Left && a.Top <= b.Bottom && a.Bottom >= b.Top;

    /// <summary>
    /// Returns the smallest rectangle that contains both <paramref name="a"/> and <paramref name="b"/>.
    /// </summary>
    public static PdfRectangle Union(in PdfRectangle a, in PdfRectangle b)
    {
        return new PdfRectangle(
            Math.Min(a.Left, b.Left),
            Math.Min(a.Top, b.Top),
            Math.Max(a.Right, b.Right),
            Math.Max(a.Bottom, b.Bottom));
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"[{Left.ToString(CultureInfo.InvariantCulture)} {Top.ToString(CultureInfo.InvariantCulture)} {Right.ToString(CultureInfo.InvariantCulture)} {Bottom.ToString(CultureInfo.InvariantCulture)}]";
}
