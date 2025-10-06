using System;
using System.Numerics;
using PdfReader.Models;

namespace PdfReader.Icc
{
    /// <summary>
    /// Delegate representing core CMYK (device) -> sRGB conversion used to populate a 4D LUT.
    /// Returns true when conversion succeeds and outputs an sRGB (0..1) triplet.
    /// </summary>
    /// <param name="c">Cyan component (0..1).</param>
    /// <param name="m">Magenta component (0..1).</param>
    /// <param name="y">Yellow component (0..1).</param>
    /// <param name="k">Black component (0..1).</param>
    /// <param name="intent">Rendering intent for which the conversion is evaluated.</param>
    /// <param name="srgb01">Resulting sRGB color (0..1) when method returns true.</param>
    /// <returns>True on success; false if conversion not supported for provided values.</returns>
    internal delegate bool CmykDeviceToSrgbCore(float c, float m, float y, float k, PdfRenderingIntent intent, out Vector3 srgb01);

    /// <summary>
    /// 4D CMYK -> sRGB lookup table helper (experimental preview acceleration for ICC CMYK profiles).
    /// Stores a uniform GridSize^4 lattice of sRGB samples (Vector4 per point; W currently unused/reserved).
    /// Provides multilinear interpolation across all four axes and returns Vector3 sRGB results.
    /// </summary>
    internal static class IccCmyk4dLut
    {
        /// <summary>
        /// Grid point count per CMYK dimension. 15 gives 15^4 = 50,625 samples (~0.77 MB @ 16 bytes / Vector4).
        /// 17 gives 83,521 samples (~1.27 MB). Adjust if memory vs. quality trade changes.
        /// </summary>
        internal const int GridSize = 15;

        /// <summary>
        /// Build a 4D CMYK -> sRGB LUT for the specified intent using the provided converter delegate.
        /// Returns null if every sample conversion fails (signals caller to fall back to analytic path).
        /// </summary>
        /// <param name="intent">Rendering intent target.</param>
        /// <param name="converter">Delegate performing analytic CMYK -> sRGB conversion.</param>
        /// <returns>Vector4 array (length = GridSize^4) or null if all samples failed.</returns>
        internal static Vector4[] Build(PdfRenderingIntent intent, CmykDeviceToSrgbCore converter)
        {
            if (converter == null)
            {
                return null;
            }

            int n = GridSize;
            int total = n * n * n * n;
            Vector4[] lut = new Vector4[total];
            int writeIndex = 0;
            bool anySuccess = false;

            for (int cIndex = 0; cIndex < n; cIndex++)
            {
                float c = (float)cIndex / (n - 1);
                for (int mIndex = 0; mIndex < n; mIndex++)
                {
                    float m = (float)mIndex / (n - 1);
                    for (int yIndex = 0; yIndex < n; yIndex++)
                    {
                        float y = (float)yIndex / (n - 1);
                        for (int kIndex = 0; kIndex < n; kIndex++)
                        {
                            float k = (float)kIndex / (n - 1);
                            if (!converter(c, m, y, k, intent, out Vector3 srgb))
                            {
                                srgb = Vector3.Zero;
                            }
                            else
                            {
                                anySuccess = true;
                            }
                            // Store as Vector4 for potential future alpha / padding usage; W left at 1 for now.
                            lut[writeIndex++] = new Vector4(srgb.X, srgb.Y, srgb.Z, 1f);
                        }
                    }
                }
            }

            if (!anySuccess)
            {
                return null;
            }

            return lut;
        }

        /// <summary>
        /// Multilinear (4D) interpolation over the CMYK unit hypercube. Weights all 16 hypercube corners.
        /// </summary>
        /// <param name="lut">Vector4 LUT (length GridSize^4).</param>
        /// <param name="c">Cyan 0..1.</param>
        /// <param name="m">Magenta 0..1.</param>
        /// <param name="y">Yellow 0..1.</param>
        /// <param name="k">Black 0..1.</param>
        /// <returns>Interpolated sRGB Vector3 (clamped to 0..1 implicitly by source samples).</returns>
        internal static Vector3 SampleMultilinear(Vector4[] lut, float c, float m, float y, float k)
        {
            int n = GridSize;
            float cf = c * (n - 1);
            float mf = m * (n - 1);
            float yf = y * (n - 1);
            float kf = k * (n - 1);

            int c0 = (int)cf;
            int m0 = (int)mf;
            int y0 = (int)yf;
            int k0 = (int)kf;

            if (c0 >= n - 1) { c0 = n - 2; cf = n - 1; }
            if (m0 >= n - 1) { m0 = n - 2; mf = n - 1; }
            if (y0 >= n - 1) { y0 = n - 2; yf = n - 1; }
            if (k0 >= n - 1) { k0 = n - 2; kf = n - 1; }

            int c1 = c0 + 1;
            int m1 = m0 + 1;
            int y1 = y0 + 1;
            int k1 = k0 + 1;

            float dc = cf - c0;
            float dm = mf - m0;
            float dy = yf - y0;
            float dk = kf - k0;

            int Index(int ci, int mi, int yi, int ki)
            {
                return (((ci * n) + mi) * n + yi) * n + ki;
            }

            // Accumulate using weight products for the 16 corners.
            Vector4 accum = Vector4.Zero;

            for (int ci = 0; ci < 2; ci++)
            {
                float wc = ci == 0 ? (1f - dc) : dc;
                int cc = ci == 0 ? c0 : c1;
                for (int mi = 0; mi < 2; mi++)
                {
                    float wm = mi == 0 ? (1f - dm) : dm;
                    int mm = mi == 0 ? m0 : m1;
                    float wcm = wc * wm;
                    for (int yi = 0; yi < 2; yi++)
                    {
                        float wyc = yi == 0 ? (1f - dy) : dy;
                        int yy = yi == 0 ? y0 : y1;
                        float wcmY = wcm * wyc;
                        for (int ki = 0; ki < 2; ki++)
                        {
                            float wk = ki == 0 ? (1f - dk) : dk;
                            int kk = ki == 0 ? k0 : k1;
                            float weight = wcmY * wk;
                            accum += lut[Index(cc, mm, yy, kk)] * weight;
                        }
                    }
                }
            }

            return new Vector3(accum.X, accum.Y, accum.Z);
        }
    }
}
