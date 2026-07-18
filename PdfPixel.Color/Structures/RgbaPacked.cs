using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Color.Structures;

/// <summary>
/// Represents an RGBA color as four tightly-packed bytes in R, G, B, A order.
/// Suitable for direct memory mapping over 32-bit pixel buffers.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RgbaPacked : IEquatable<RgbaPacked>
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
    /// Alpha channel (0 = fully transparent, 255 = fully opaque).
    /// </summary>
    public byte A;

    /// <summary>
    /// Initializes a new <see cref="RgbaPacked"/> with the given channel values.
    /// </summary>
    public RgbaPacked(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is RgbaPacked other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RgbaPacked other) => R == other.R && G == other.G && B == other.B && A == other.A;

    /// <summary>
    /// Returns <see langword="true"/> if both values have identical channel bytes.
    /// </summary>
    public static bool operator ==(RgbaPacked rgba1, RgbaPacked rgba2) => rgba1.Equals(rgba2);

    /// <summary>
    /// Returns <see langword="true"/> if the values differ in any channel byte.
    /// </summary>
    public static bool operator !=(RgbaPacked rgba1, RgbaPacked rgba2) => !rgba1.Equals(rgba2);

    /// <inheritdoc/>
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}
