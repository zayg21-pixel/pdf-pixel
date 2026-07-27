using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PdfPixel.Geometry;

/// <summary>
/// An integer-valued point defined by its X and Y coordinates.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PdfIntegerPoint : IEquatable<PdfIntegerPoint>
{
    /// <summary>
    /// Initializes a new <see cref="PdfIntegerPoint"/> from its coordinates.
    /// </summary>
    public PdfIntegerPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// X coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Y coordinate.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// The point at the origin.
    /// </summary>
    public static PdfIntegerPoint Empty { get; } = new(0, 0);

    /// <summary>
    /// Whether this point equals <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => this == Empty;

    /// <inheritdoc/>
    public bool Equals(PdfIntegerPoint other) => X == other.X && Y == other.Y;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfIntegerPoint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>
    /// Determines whether two points have the same coordinates.
    /// </summary>
    public static bool operator ==(in PdfIntegerPoint left, in PdfIntegerPoint right) => left.Equals(right);

    /// <summary>
    /// Determines whether two points have different coordinates.
    /// </summary>
    public static bool operator !=(in PdfIntegerPoint left, in PdfIntegerPoint right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{X.ToString(CultureInfo.InvariantCulture)} {Y.ToString(CultureInfo.InvariantCulture)}]";
}
