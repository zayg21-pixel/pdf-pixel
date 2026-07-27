using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PdfPixel.Geometry;

/// <summary>
/// An integer-valued size defined by its width and height.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PdfIntegerSize : IEquatable<PdfIntegerSize>
{
    /// <summary>
    /// Initializes a new <see cref="PdfIntegerSize"/> from its dimensions.
    /// </summary>
    public PdfIntegerSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// The zero-sized instance.
    /// </summary>
    public static PdfIntegerSize Empty { get; } = new(0, 0);

    /// <summary>
    /// Whether this size equals <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => this == Empty;

    /// <inheritdoc/>
    public bool Equals(PdfIntegerSize other) => Width == other.Width && Height == other.Height;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfIntegerSize other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Width, Height);

    /// <summary>
    /// Determines whether two sizes have the same dimensions.
    /// </summary>
    public static bool operator ==(in PdfIntegerSize left, in PdfIntegerSize right) => left.Equals(right);

    /// <summary>
    /// Determines whether two sizes have different dimensions.
    /// </summary>
    public static bool operator !=(in PdfIntegerSize left, in PdfIntegerSize right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{Width.ToString(CultureInfo.InvariantCulture)} {Height.ToString(CultureInfo.InvariantCulture)}]";
}
