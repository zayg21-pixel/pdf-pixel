using PdfPixel.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PdfPixel.Geometry;

/// <summary>
/// A rectangle defined by its left, top, right, and bottom edges.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PdfRectangle : IEquatable<PdfRectangle>
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
    public bool IsEmpty => this == Empty;

    /// <summary>
    /// Creates a rectangle with its top-left corner at <paramref name="left"/>, <paramref name="top"/>
    /// and the given <paramref name="width"/> and <paramref name="height"/>.
    /// </summary>
    public static PdfRectangle FromLocationAndSize(float left, float top, float width, float height)
        => new(left, top, left + width, top + height);

    /// <summary>
    /// Creates a rectangle with its top-left corner at <paramref name="location"/> and the given <paramref name="size"/>.
    /// </summary>
    public static PdfRectangle FromLocationAndSize(in PdfPoint location, in PdfSize size)
        => new(location.X, location.Y, location.X + size.Width, location.Y + size.Height);

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
    /// Creates the smallest <see cref="PdfRectangle"/> containing every point in <paramref name="points"/>.
    /// Returns null when <paramref name="points"/> is empty.
    /// </summary>
    public static PdfRectangle? FromPoints(IEnumerable<PdfPoint> points)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        var hasBounds = false;
        float left = 0;
        float top = 0;
        float right = 0;
        float bottom = 0;

        foreach (PdfPoint point in points)
        {
            if (!hasBounds)
            {
                left = point.X;
                right = point.X;
                top = point.Y;
                bottom = point.Y;
                hasBounds = true;
                continue;
            }

            left = Math.Min(left, point.X);
            right = Math.Max(right, point.X);
            top = Math.Min(top, point.Y);
            bottom = Math.Max(bottom, point.Y);
        }

        return hasBounds ? new PdfRectangle(left, top, right, bottom) : null;
    }

    /// <summary>
    /// Returns the overlapping region of <paramref name="a"/> and <paramref name="b"/>, or <see cref="Empty"/> when they do not overlap.
    /// </summary>
    public static PdfRectangle Intersect(in PdfRectangle a, in PdfRectangle b)
    {
        if (!IntersectsWith(a, b))
        {
            return Empty;
        }

        return new PdfRectangle(
            Math.Max(a.Left, b.Left),
            Math.Max(a.Top, b.Top),
            Math.Min(a.Right, b.Right),
            Math.Min(a.Bottom, b.Bottom));
    }

    /// <summary>
    /// Whether <paramref name="a"/> and <paramref name="b"/> overlap, counting rectangles that only
    /// share an edge as overlapping.
    /// </summary>
    public static bool IntersectsWith(in PdfRectangle a, in PdfRectangle b)
        => a.Left <= b.Right && a.Right >= b.Left && a.Top <= b.Bottom && a.Bottom >= b.Top;

    /// <summary>
    /// Whether <paramref name="point"/> lies within this rectangle, counting the left and top edges
    /// but not the right and bottom ones.
    /// </summary>
    public bool Contains(in PdfPoint point)
        => point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    /// <summary>
    /// Whether <paramref name="rect"/> lies entirely within this rectangle, edges included.
    /// </summary>
    public bool Contains(in PdfRectangle rect)
        => Left <= rect.Left && Top <= rect.Top && Right >= rect.Right && Bottom >= rect.Bottom;

    /// <summary>
    /// Returns this rectangle grown by <paramref name="amount"/> on every edge.
    /// </summary>
    public PdfRectangle Inflate(float amount) => new(Left - amount, Top - amount, Right + amount, Bottom + amount);

    /// <summary>
    /// Returns the smallest rectangle that contains both <paramref name="a"/> and <paramref name="b"/>.
    /// </summary>
    public static PdfRectangle Union(in PdfRectangle a, in PdfRectangle b)
    {
        return new(
            Math.Min(a.Left, b.Left),
            Math.Min(a.Top, b.Top),
            Math.Max(a.Right, b.Right),
            Math.Max(a.Bottom, b.Bottom));
    }

    /// <inheritdoc/>
    public bool Equals(PdfRectangle other)
        => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfRectangle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    /// <summary>
    /// Determines whether two rectangles have the same edges.
    /// </summary>
    public static bool operator ==(in PdfRectangle left, in PdfRectangle right) => left.Equals(right);

    /// <summary>
    /// Determines whether two rectangles have different edges.
    /// </summary>
    public static bool operator !=(in PdfRectangle left, in PdfRectangle right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{Left.ToString(CultureInfo.InvariantCulture)} {Top.ToString(CultureInfo.InvariantCulture)} {Right.ToString(CultureInfo.InvariantCulture)} {Bottom.ToString(CultureInfo.InvariantCulture)}]";
}
