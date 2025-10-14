using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfReader.Rendering.Color.Clut
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Rgba : IEquatable<Rgba>
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public Rgba(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return HashCode.Combine(R, G, B, A);
        }

        public override bool Equals(object obj)
        {
            return obj is Rgba other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Rgba other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Rgb : IEquatable<Rgb>
    {
        public byte R;
        public byte G;
        public byte B;

        public Rgb(byte r, byte g, byte b)
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
            return obj is Rgba other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Rgb other)
        {
            return R == other.R && G == other.G && B == other.B ;
        }
    }
}
