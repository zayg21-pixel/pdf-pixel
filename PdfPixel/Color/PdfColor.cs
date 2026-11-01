using System;
using System.Globalization;
using System.IO.Hashing;
using System.Linq;
using System.Runtime.InteropServices;

namespace PdfPixel.Color;

/// <summary>
/// The component values of a color, in the units of the color space they are given in.
/// </summary>
public readonly struct PdfColor : IEquatable<PdfColor>
{
    /// <summary>
    /// Initializes a new <see cref="PdfColor"/> over <paramref name="components"/>.
    /// </summary>
    public PdfColor(in ReadOnlyMemory<float> components) => Components = components;

    /// <summary>
    /// Initializes a new <see cref="PdfColor"/> over <paramref name="components"/>.
    /// </summary>
    public PdfColor(float[] components) => Components = components;

    /// <summary>
    /// Gets the component values.
    /// </summary>
    public ReadOnlyMemory<float> Components { get; }

    /// <summary>
    /// Gets a color with no components.
    /// </summary>
    public static PdfColor Empty => new(ReadOnlyMemory<float>.Empty);

    /// <inheritdoc/>
    public bool Equals(PdfColor other) => Components.Span.SequenceEqual(other.Components.Span);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is PdfColor other)
        {
            return Equals(other);
        }

        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => (int)XxHash32.HashToUInt32(MemoryMarshal.AsBytes(Components.Span));

    /// <summary>
    /// Equality operator for <see cref="PdfColor"/>.
    /// </summary>
    public static bool operator ==(in PdfColor left, in PdfColor right) => left.Equals(right);

    /// <summary>
    /// Inequality operator for <see cref="PdfColor"/>.
    /// </summary>
    public static bool operator !=(in PdfColor left, in PdfColor right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => string.Join(" ", Components.ToArray().Select(component => component.ToString(CultureInfo.InvariantCulture)));
}
