using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Color.Structures;

/// <summary>
/// Packed representation of an RGB color.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RgbPacked : IEquatable<RgbPacked>
{
    public byte R;
    public byte G;
    public byte B;

    public RgbPacked(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        return HashCode.Combine(R, G, B);
    }

    public override bool Equals(object obj)
    {
        return obj is RgbaPacked other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RgbPacked other)
    {
        return R == other.R && G == other.G && B == other.B ;
    }
}
