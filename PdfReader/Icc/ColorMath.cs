using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using PdfReader.Models;

namespace PdfReader.Icc
{
    internal static class ColorMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 FromXyzD50ToSrgb01(in Vector3 xyz)
        {
            // XYZ(D50) -> linear sRGB using precomputed rows
            float rl = Vector3.Dot(IccProfileHelpers.D50ToSrgbRow0, xyz);
            float gl = Vector3.Dot(IccProfileHelpers.D50ToSrgbRow1, xyz);
            float bl = Vector3.Dot(IccProfileHelpers.D50ToSrgbRow2, xyz);

            // Compand via LUT (linear interpolation)
            float r = LookupLinear(IccProfileHelpers.SrgbCompLut, rl);
            float g = LookupLinear(IccProfileHelpers.SrgbCompLut, gl);
            float b = LookupLinear(IccProfileHelpers.SrgbCompLut, bl);

            return Vector3.Clamp(new Vector3(r, g, b), Vector3.Zero, Vector3.One);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LookupLinear(float[] table, float x, float domainMaxInv = 1f)
        {
            if (table == null || table.Length == 0)
            {
                return x;
            }

            int lastIndex = table.Length - 1;

            // Normalize x to [0..1] using precomputed inverse of domain max
            float normalized = x * domainMaxInv;

            if (normalized <= 0f)
            {
                return table[0];
            }

            if (normalized >= 1f)
            {
                return table[lastIndex];
            }

            float position = normalized * lastIndex;
            int index0 = (int)Math.Floor(position);
            int index1 = index0 + 1;
            float fraction = position - index0;

            float v0 = table[index0];
            float v1 = table[index1];

            return v0 + (v1 - v0) * fraction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ApplyBlackPointCompensation(in Vector3 xyzD50, float srcBlackL, float bpcScale, PdfRenderingIntent intent)
        {
            if (intent != PdfRenderingIntent.RelativeColorimetric)
            {
                return xyzD50;
            }

            if (srcBlackL <= 0f)
            {
                return xyzD50;
            }

            var lab = XyzD50ToLab(in xyzD50);
            float L2 = (lab.X - srcBlackL) * bpcScale;
            if (L2 < 0f)
            {
                L2 = 0f;
            }
            else if (L2 > 100f)
            {
                L2 = 100f;
            }

            return LabD50ToXyz(L2, lab.Y, lab.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 LabD50ToXyz(float Lstar, float astar, float bstar)
        {
            float fy = (Lstar + 16f) / 116f;
            float fx = fy + (astar / 500f);
            float fz = fy - (bstar / 200f);

            var f = new Vector3(fx, fy, fz);
            var r = InvFVec(in f);

            return r * IccProfileHelpers.D50WhitePoint;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 InvFVec(in Vector3 f)
        {
            float fx = f.X;
            float fy = f.Y;
            float fz = f.Z;

            float x3 = fx * fx * fx;
            float y3 = fy * fy * fy;
            float z3 = fz * fz * fz;

            float xr = (x3 >= IccProfileHelpers.LabEpsilon) ? x3 : (fx - IccProfileHelpers.LabLinearB) / IccProfileHelpers.LabLinearA;
            float yr = (y3 >= IccProfileHelpers.LabEpsilon) ? y3 : (fy - IccProfileHelpers.LabLinearB) / IccProfileHelpers.LabLinearA;
            float zr = (z3 >= IccProfileHelpers.LabEpsilon) ? z3 : (fz - IccProfileHelpers.LabLinearB) / IccProfileHelpers.LabLinearA;

            return new Vector3(xr, yr, zr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 XyzD50ToLab(in Vector3 xyz)
        {
            var t = xyz * IccProfileHelpers.D50WhitePointInverse;

            float fx = CbrtFromLut(t.X);
            float fy = CbrtFromLut(t.Y);
            float fz = CbrtFromLut(t.Z);

            float L = 116f * fy - 16f;
            float a = 500f * (fx - fy);
            float b = 200f * (fy - fz);

            return new Vector3(L, a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CbrtFromLut(float t)
        {
            if (t < IccProfileHelpers.LabEpsilon)
            {
                return IccProfileHelpers.LabLinearA * t + IccProfileHelpers.LabLinearB;
            }

            return LookupLinear(IccProfileHelpers.CbrtLut, t, IccProfileHelpers.CbrtLutMaxInv);
        }
    }
}
