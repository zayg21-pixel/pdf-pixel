using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Color.Structures;

/// <summary>
/// Represents an RGB color as three tightly-packed bytes in R, G, B order, with no alpha.
/// Suitable for direct memory mapping over 24-bit pixel buffers.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RgbPacked : IEquatable<RgbPacked>
{
    /// <summary>
    /// Red channel (0–255).
    /// </summary>
    public byte R;

    /// <summary>
    /// Green channel (0–255).
    /// </summary>
    public byte G;

    /// <summary>
    /// Blue channel (0–255).
    /// </summary>
    public byte B;

    /// <summary>
    /// Initializes a new <see cref="RgbPacked"/> with the given channel values.
    /// </summary>
    public RgbPacked(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(R, G, B);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RgbPacked other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RgbPacked other) => R == other.R && G == other.G && B == other.B;

    /// <summary>
    /// Returns <see langword="true"/> if both values have identical channel bytes.
    /// </summary>
    public static bool operator ==(RgbPacked rgb1, RgbPacked rgb2) => rgb1.Equals(rgb2);

    /// <summary>
    /// Returns <see langword="true"/> if the values differ in any channel byte.
    /// </summary>
    public static bool operator !=(RgbPacked rgb1, RgbPacked rgb2) => !rgb1.Equals(rgb2);

    /// <inheritdoc/>
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}
