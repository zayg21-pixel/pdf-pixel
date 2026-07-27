using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Geometry;

/// <summary>
/// A size defined by its width and height.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PdfSize : IEquatable<PdfSize>
{
    /// <summary>
    /// Initializes a new <see cref="PdfSize"/> from its dimensions.
    /// </summary>
    public PdfSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Width.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Height.
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// The zero-sized instance.
    /// </summary>
    public static PdfSize Empty { get; } = new(0, 0);

    /// <inheritdoc/>
    public bool Equals(PdfSize other) => Width == other.Width && Height == other.Height;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfSize other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Width, Height);

    /// <summary>
    /// Determines whether two sizes have the same dimensions.
    /// </summary>
    public static bool operator ==(in PdfSize left, in PdfSize right) => left.Equals(right);

    /// <summary>
    /// Determines whether two sizes have different dimensions.
    /// </summary>
    public static bool operator !=(in PdfSize left, in PdfSize right) => !left.Equals(right);
}
