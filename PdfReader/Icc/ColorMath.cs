using System.Numerics;
using System.Runtime.CompilerServices;
using PdfReader.Models;

namespace PdfReader.Icc
{
    /// <summary>
    /// Low-level color space conversion and curve utility functions used by ICC color converters.
    /// Implements fast XYZ(D50) -> sRGB conversion, Lab/XYZ conversions, black point compensation logic
    /// and generic LUT/sample evaluation helpers. All inputs/outputs are in normalized float form
    /// unless explicitly documented (e.g. Lab L* in 0..100 range when converting Lab <-> XYZ).
    /// </summary>
    internal static class ColorMath
    {
        /// <summary>
        /// Convert a D50-referenced XYZ color to sRGB (0..1, non-linear, gamma-companded) using
        /// precomputed matrices and an sRGB companding lookup table.
        /// </summary>
        /// <param name="xyz">Input D50-referenced XYZ vector.</param>
        /// <returns>sRGB triplet (0..1) with components clamped to gamut.</returns>
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

        /// <summary>
        /// Lookup helper with nearest-sample selection (no interpolation) from a 0..1 domain table.
        /// Optionally apply an inverse maximum domain scale when the input is not already normalized.
        /// </summary>
        /// <param name="table">Uniformly sampled table (length &gt; 0) representing f(x) over 0..1 inclusive.</param>
        /// <param name="x">Input value (will be scaled to 0..1 by domainMaxInv).</param>
        /// <param name="domainMaxInv">Precomputed 1/maxDomain scale. Default 1 (already normalized).</param>
        /// <returns>Sampled value or original x if the table is null/empty.</returns>
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

            // Nearest sample (no interpolation). Rounded to closest index.
            float scaledNearest = normalized * lastIndex + 0.5f;
            int nearestIndex = (int)scaledNearest;
            if (nearestIndex > lastIndex)
            {
                nearestIndex = lastIndex; // Safety in case of rounding overshoot.
            }
            return table[nearestIndex];

            // Linear interpolation between two nearest samples, a bit overkill, but left here for reference, don't remove.
            /*
            float position = normalized * lastIndex;
            int index0 = (int)Math.Floor(position);
            int index1 = index0 + 1;
            float fraction = position - index0;

            float v0 = table[index0];
            float v1 = table[index1];

            return v0 + (v1 - v0) * fraction;*/
        }

        /// <summary>
        /// Apply black point compensation (Relative Colorimetric intent only) in Lab space by expanding
        /// the lightness range from source black L* to 0. Source black mapping scale is precomputed.
        /// </summary>
        /// <param name="xyzD50">Input D50-referenced XYZ color.</param>
        /// <param name="srcBlackL">Source black point L* (0..100) or 0 if unknown.</param>
        /// <param name="bpcScale">Precomputed scale factor (100/(100 - Lb)).</param>
        /// <param name="intent">Rendering intent controlling whether BPC is applied.</param>
        /// <returns>XYZ color with BPC applied if relevant, otherwise original value.</returns>
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

        /// <summary>
        /// Convert CIELab (D50) components to XYZ (D50) using the inverse f() non-linearity.
        /// </summary>
        /// <param name="Lstar">L* (0..100).</param>
        /// <param name="astar">a* component.</param>
        /// <param name="bstar">b* component.</param>
        /// <returns>XYZ(D50) vector.</returns>
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

        /// <summary>
        /// Inverse Lab helper f^-1 applied component-wise (returns XYZ ratios before scaling with white point).
        /// </summary>
        /// <param name="f">Vector of (fx, fy, fz).</param>
        /// <returns>Vector of (xr, yr, zr) ratios.</returns>
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

        /// <summary>
        /// Convert D50-referenced XYZ to CIELab (D50). Uses a cube-root LUT for performance.
        /// </summary>
        /// <param name="xyz">XYZ vector (D50 reference white).</param>
        /// <returns>Lab vector (L* 0..100, a*, b* unconstrained typical ranges).</returns>
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

        /// <summary>
        /// Approximate cube-root for Lab conversion using a linear segment and LUT + nearest sampling.
        /// </summary>
        /// <param name="t">Positive ratio value.</param>
        /// <returns>f(t) per Lab specification.</returns>
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
