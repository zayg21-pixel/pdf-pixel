using System;
using System.Globalization;

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
    /// Parses an HTML hex color - <c>#RRGGBB</c> or <c>#RRGGBBAA</c>, with or without the leading hash.
    /// </summary>
    /// <param name="value">The text to parse.</param>
    /// <returns>The parsed color, opaque when the text carries no alpha channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="value"/> is not an HTML hex color.</exception>
    public static PdfColor ParseHexColor(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string hex = (value.Length > 0 && value[0] == '#') ? value.Substring(1) : value;

        if (hex.Length != 6 && hex.Length != 8)
        {
            throw new FormatException($"'{value}' is not an HTML hex color.");
        }

        byte red = ParseHexByte(hex, 0);
        byte green = ParseHexByte(hex, 2);
        byte blue = ParseHexByte(hex, 4);
        byte alpha = (hex.Length == 8) ? ParseHexByte(hex, 6) : byte.MaxValue;

        return new PdfColor(red / 255f, green / 255f, blue / 255f, alpha / 255f);
    }

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

    private static byte ParseHexByte(string hex, int index)
    {
#if NETSTANDARD2_0
        return byte.Parse(hex.Substring(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
#else
        return byte.Parse(hex.AsSpan(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
#endif
    }
}
