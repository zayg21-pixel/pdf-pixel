using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// A point in font space, defined by its X and Y coordinates.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PdfFontPoint : IEquatable<PdfFontPoint>
{
    /// <summary>
    /// Initializes a new <see cref="PdfFontPoint"/> from its coordinates.
    /// </summary>
    public PdfFontPoint(float x, float y)
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

    /// <inheritdoc/>
    public bool Equals(PdfFontPoint other) => X == other.X && Y == other.Y;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfFontPoint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>
    /// Determines whether two points have the same coordinates.
    /// </summary>
    public static bool operator ==(in PdfFontPoint left, in PdfFontPoint right) => left.Equals(right);

    /// <summary>
    /// Determines whether two points have different coordinates.
    /// </summary>
    public static bool operator !=(in PdfFontPoint left, in PdfFontPoint right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{X.ToString(CultureInfo.InvariantCulture)} {Y.ToString(CultureInfo.InvariantCulture)}]";
}
