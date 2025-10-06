using System;
using System.Numerics;
using PdfReader.Models;

namespace PdfReader.Icc
{
    /// <summary>
    /// Interpolation mode selector for 3D LUT sampling.
    /// </summary>
    internal enum SamlingInterpolation
    {
        /// <summary>
        /// Bilinear interpolation in the R,G plane with nearest neighbor selection on the B axis.
        /// </summary>
        SampleBilinear,
        /// <summary>
        /// Full trilinear interpolation across R,G,B axes.
        /// </summary>
        SampleTrilinear
    }

    /// <summary>
    /// Delegate representing the core device (RGB or Lab) -> sRGB conversion used to populate LUT samples.
    /// Returns true when conversion succeeds and sets <paramref name="srgb01"/>.
    /// </summary>
    /// <param name="c0">First device component (0..1).</param>
    /// <param name="c1">Second device component (0..1).</param>
    /// <param name="c2">Third device component (0..1).</param>
    /// <param name="intent">Rendering intent.</param>
    /// <param name="srgb01">Resulting sRGB triplet (0..1).</param>
    /// <returns>True on success; false if conversion unsupported for provided components.</returns>
    internal delegate bool RgbDeviceToSrgbCore(float c0, float c1, float c2, PdfRenderingIntent intent, out Vector3 srgb01);

    /// <summary>
    /// Helper for building and sampling 3D device->sRGB lookup tables.
    /// Provides bilinear (RG + nearest B) and full trilinear interpolation methods.
    /// </summary>
    internal static class IccRgb3dLut
    {
        /// <summary>
        /// Grid point count per dimension (fixed). 17^3 ~= 4913 samples.
        /// </summary>
        internal const int GridSize = 17;

        /// <summary>
        /// Build a 3D LUT for the specified intent using the supplied converter delegate.
        /// Returns null if all sample conversions fail (signals that LUT should not be used).
        /// </summary>
        internal static Vector3[] Build(PdfRenderingIntent intent, RgbDeviceToSrgbCore converter)
        {
            if (converter == null)
            {
                return null;
            }

            int n = GridSize;
            int total = n * n * n;
            Vector3[] lut = new Vector3[total];
            int writeIndex = 0;
            bool anySuccess = false;

            for (int rIndex = 0; rIndex < n; rIndex++)
            {
                float r = (float)rIndex / (n - 1);
                for (int gIndex = 0; gIndex < n; gIndex++)
                {
                    float g = (float)gIndex / (n - 1);
                    for (int bIndex = 0; bIndex < n; bIndex++)
                    {
                        float b = (float)bIndex / (n - 1);
                        if (!converter(r, g, b, intent, out Vector3 srgb))
                        {
                            srgb = Vector3.Zero;
                        }
                        else
                        {
                            anySuccess = true;
                        }
                        lut[writeIndex++] = srgb;
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
        /// Public sampling entry point selecting interpolation strategy.
        /// </summary>
        internal static Vector3 Sample(Vector3[] lut, float r, float g, float b, SamlingInterpolation mode)
        {
            if (lut == null || lut.Length == 0)
            {
                return Vector3.Zero;
            }

            switch (mode)
            {
                case SamlingInterpolation.SampleTrilinear:
                    return SampleTrilinear(lut, r, g, b);
                case SamlingInterpolation.SampleBilinear:
                default:
                    return SampleBilinear(lut, r, g, b);
            }
        }

        // Bilinear interpolation on R,G plane with nearest neighbor selection in B (private implementation).
        private static Vector3 SampleBilinear(Vector3[] lut, float r, float g, float b)
        {
            int n = GridSize;
            float rf = r * (n - 1);
            float gf = g * (n - 1);
            float bf = b * (n - 1);

            int r0 = (int)rf;
            int g0 = (int)gf;
            int b0 = (int)bf;

            if (r0 >= n - 1)
            {
                r0 = n - 2;
                rf = n - 1;
            }
            if (g0 >= n - 1)
            {
                g0 = n - 2;
                gf = n - 1;
            }
            if (b0 >= n - 1)
            {
                b0 = n - 2;
                bf = n - 1;
            }

            int r1 = r0 + 1;
            int g1 = g0 + 1;
            int b1 = b0 + 1;

            float dr = rf - r0;
            float dg = gf - g0;
            float db = bf - b0;

            int bSlice = db < 0.5f ? b0 : b1;

            int SliceIndex(int ri, int gi, int bi)
            {
                return (ri * n + gi) * n + bi;
            }

            Vector3 c00 = lut[SliceIndex(r0, g0, bSlice)];
            Vector3 c10 = lut[SliceIndex(r1, g0, bSlice)];
            Vector3 c01 = lut[SliceIndex(r0, g1, bSlice)];
            Vector3 c11 = lut[SliceIndex(r1, g1, bSlice)];

            Vector3 c0 = Vector3.Lerp(c00, c10, dr);
            Vector3 c1 = Vector3.Lerp(c01, c11, dr);
            Vector3 c = Vector3.Lerp(c0, c1, dg);
            return c;
        }

        // Full trilinear interpolation across R,G,B axes (private implementation).
        private static Vector3 SampleTrilinear(Vector3[] lut, float r, float g, float b)
        {
            int n = GridSize;
            float rf = r * (n - 1);
            float gf = g * (n - 1);
            float bf = b * (n - 1);

            int r0 = (int)rf;
            int g0 = (int)gf;
            int b0 = (int)bf;

            if (r0 >= n - 1)
            {
                r0 = n - 2;
                rf = n - 1;
            }
            if (g0 >= n - 1)
            {
                g0 = n - 2;
                gf = n - 1;
            }
            if (b0 >= n - 1)
            {
                b0 = n - 2;
                bf = n - 1;
            }

            int r1 = r0 + 1;
            int g1 = g0 + 1;
            int b1 = b0 + 1;

            float dr = rf - r0;
            float dg = gf - g0;
            float db = bf - b0;

            int SliceIndex(int ri, int gi, int bi)
            {
                return (ri * n + gi) * n + bi;
            }

            Vector3 c000 = lut[SliceIndex(r0, g0, b0)];
            Vector3 c100 = lut[SliceIndex(r1, g0, b0)];
            Vector3 c010 = lut[SliceIndex(r0, g1, b0)];
            Vector3 c110 = lut[SliceIndex(r1, g1, b0)];
            Vector3 c001 = lut[SliceIndex(r0, g0, b1)];
            Vector3 c101 = lut[SliceIndex(r1, g0, b1)];
            Vector3 c011 = lut[SliceIndex(r0, g1, b1)];
            Vector3 c111 = lut[SliceIndex(r1, g1, b1)];

            Vector3 c00 = Vector3.Lerp(c000, c100, dr);
            Vector3 c10 = Vector3.Lerp(c010, c110, dr);
            Vector3 c01 = Vector3.Lerp(c001, c101, dr);
            Vector3 c11 = Vector3.Lerp(c011, c111, dr);
            Vector3 c0 = Vector3.Lerp(c00, c10, dg);
            Vector3 c1 = Vector3.Lerp(c01, c11, dg);
            Vector3 c = Vector3.Lerp(c0, c1, db);
            return c;
        }
    }
}
