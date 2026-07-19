using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color;

/// <summary>
/// An RGBA color with channel values in the range [0, 1].
/// </summary>
public readonly struct PdfColor : IEquatable<PdfColor>
{
    /// <summary>
    /// Initializes a new <see cref="PdfColor"/> from its RGBA channel values, each in the range [0, 1].
    /// </summary>
    public PdfColor(float red, float green, float blue, float alpha = 1f)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    /// <summary>
    /// Red channel, in the range [0, 1].
    /// </summary>
    public float Red { get; }

    /// <summary>
    /// Green channel, in the range [0, 1].
    /// </summary>
    public float Green { get; }

    /// <summary>
    /// Blue channel, in the range [0, 1].
    /// </summary>
    public float Blue { get; }

    /// <summary>
    /// Alpha channel, in the range [0, 1].
    /// </summary>
    public float Alpha { get; }

    /// <summary>
    /// Returns a copy of this color with the alpha channel replaced.
    /// </summary>
    public PdfColor WithAlpha(float alpha) => new(Red, Green, Blue, alpha);

    /// <summary>
    /// Converts this color to an <see cref="SKColor"/>.
    /// </summary>
    internal SKColor ToSkiaColor()
    {
        return new(
            ToByte(Red),
            ToByte(Green),
            ToByte(Blue),
            ToByte(Alpha));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToByte(float channel) => (byte)((Math.Max(0f, Math.Min(1f, channel)) * 255f) + 0.5f);

    /// <inheritdoc/>
    public bool Equals(PdfColor other) => Red == other.Red && Green == other.Green && Blue == other.Blue && Alpha == other.Alpha;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfColor other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Red, Green, Blue, Alpha);

    /// <summary>
    /// Determines whether two colors have the same channel values.
    /// </summary>
    public static bool operator ==(in PdfColor left, in PdfColor right) => left.Equals(right);

    /// <summary>
    /// Determines whether two colors have different channel values.
    /// </summary>
    public static bool operator !=(in PdfColor left, in PdfColor right) => !left.Equals(right);
}
