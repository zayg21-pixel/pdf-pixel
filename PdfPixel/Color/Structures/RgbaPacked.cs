using SkiaSharp;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Color.Structures;

/// <summary>
/// Packed representation of an RGBA color.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RgbaPacked : IEquatable<RgbaPacked>
{
    public byte R;
    public byte G;
    public byte B;
    public byte A;

    public RgbaPacked(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SKColor ToSkiaColor()
    {
        return new SKColor(R, G, B, A);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        return HashCode.Combine(R, G, B, A);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object obj)
    {
        return obj is RgbaPacked other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RgbaPacked other)
    {
        return R == other.R && G == other.G && B == other.B && A == other.A;
    }
}
