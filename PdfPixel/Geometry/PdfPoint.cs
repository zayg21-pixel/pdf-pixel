using PdfPixel.Models;
using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PdfPixel.Geometry;

/// <summary>
/// A point defined by its X and Y coordinates.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PdfPoint : IEquatable<PdfPoint>
{
    /// <summary>
    /// Initializes a new <see cref="PdfPoint"/> from its coordinates.
    /// </summary>
    public PdfPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// X coordinate.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Y coordinate.
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// The point at the origin.
    /// </summary>
    public static PdfPoint Empty { get; } = new(0, 0);

    /// <summary>
    /// Whether this point equals <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => this == Empty;

    /// <summary>
    /// Creates a <see cref="PdfPoint"/> from a 2-element PDF array.
    /// Returns null if the array is not defined or has insufficient elements.
    /// </summary>
    public static PdfPoint? FromArray(PdfArray? array)
    {
        if (array == null || array.Count < 2)
        {
            return null;
        }

        return new PdfPoint(array.GetFloatOrDefault(0), array.GetFloatOrDefault(1));
    }

    /// <inheritdoc/>
    public bool Equals(PdfPoint other) => X == other.X && Y == other.Y;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfPoint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>
    /// Determines whether two points have the same coordinates.
    /// </summary>
    public static bool operator ==(in PdfPoint left, in PdfPoint right) => left.Equals(right);

    /// <summary>
    /// Determines whether two points have different coordinates.
    /// </summary>
    public static bool operator !=(in PdfPoint left, in PdfPoint right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{X.ToString(CultureInfo.InvariantCulture)} {Y.ToString(CultureInfo.InvariantCulture)}]";
}
