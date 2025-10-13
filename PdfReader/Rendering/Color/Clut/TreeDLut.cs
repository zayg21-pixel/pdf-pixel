using CoreJ2K.j2k.wavelet.synthesis;
using PdfReader.Models;
using SkiaSharp;
using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfReader.Rendering.Color.Clut
{
    internal delegate SKColor DeviceToSrgbCore(ReadOnlySpan<float> input, PdfRenderingIntent intent);

    /// <summary>
    /// Helper for building and sampling 3D device->sRGB lookup tables.
    /// LUT layout: contiguous packed RGB triples for each lattice point in (R,G,B) iteration order.
    /// Bilinear sampling (RG with nearest B) uses precomputed weight quads that sum to 1 (normalized floats).
    /// </summary>
    internal static class TreeDLut
    {
        internal const int GridSize = 17; // 17^3 = 4913 lattice points
        private const int GridIndexShift = 4; // Upper 4 bits: lattice index, lower 4 bits: fractional component
        private const int GridIndexMask = 0x0F; // Mask for fractional component (0..15)
        private const int FractionMax = 16; // Normalized fraction range (0..16)
        private const int WeightScale = 256; // Legacy fixed-point scale retained for clarity

        private const int TripleStrideG = GridSize; // Points to advance one G index
        private const int TripleStrideR = GridSize * GridSize; // Points to advance one R index
        private const int TripleStrideRG = TripleStrideR + TripleStrideG;
        private const float Inv16 = 1f / 16f; // Fraction conversion for nibble (0..15 -> 0..0.9375)

        /// <summary>
        /// Builds a packed Vector3 3D LUT (device -> sRGB) sampled uniformly over each dimension.
        /// Layout order: R (outer), G (middle), B (inner) to match existing byte LUT order for locality.
        /// Each Vector3 is stored as (R, G, B) mapping directly to pixel components.
        /// </summary>
        internal static unsafe Vector3[] BuildVectorLut(PdfRenderingIntent intent, DeviceToSrgbCore converter)
        {
            if (converter == null)
            {
                return null;
            }
            int n = GridSize;
            int totalPoints = n * n * n;
            Vector3[] lut = new Vector3[totalPoints];

            int writeIndex = 0;
            for (int rIndex = 0; rIndex < n; rIndex++)
            {
                float rNorm = (float)rIndex / (n - 1);
                for (int gIndex = 0; gIndex < n; gIndex++)
                {
                    float gNorm = (float)gIndex / (n - 1);
                    for (int bIndex = 0; bIndex < n; bIndex++)
                    {
                        float bNorm = (float)bIndex / (n - 1);
                        ReadOnlySpan<float> input = stackalloc float[] { rNorm, gNorm, bNorm };
                        SKColor color = converter(input, intent);
                        // Store as R,G,B (X=R, Y=G, Z=B).
                        lut[writeIndex] = new Vector3(color.Red, color.Green, color.Blue);
                        writeIndex++;
                    }
                }
            }
            return lut;
        }

        /// <summary>
        /// Trilinear sampling over R,G,B using the Vector3 LUT (stored as R,G,B).
        /// Performs full 3D interpolation across eight lattice corner values.
        /// This replaces the previous bilinear (R,G) + nearest B approach for smoother gradients.
        /// </summary>
        public static unsafe void SampleTrilinear(Vector3* lut, byte* rgbaRowPtr, int pixelCount)
        {
            Rgba* pixels = (Rgba*)rgbaRowPtr;
            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                Rgba* pixelPtr = pixels + pixelIndex;
                SampleTrilinear(lut, pixelPtr, pixelPtr);
            }
        }

        /// <summary>
        /// Bilinear sampling over R,G with nearest B. Normalized weights eliminate explicit normalization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SampleTrilinear(Vector3* lut, Rgba* source, Rgba* destination)
        {
            var r = source->R;
            var g = source->G;
            var b = source->B;

            int rBaseIndex = r >> GridIndexShift; // 0..15
            int gBaseIndex = g >> GridIndexShift; // 0..15
            int bBaseIndex = b >> GridIndexShift; // 0..15

            int fracR = r & GridIndexMask; // 0..15
            int fracG = g & GridIndexMask; // 0..15
            int fracB = b & GridIndexMask; // 0..15

            float fr = fracR * Inv16; // fractional R
            float fg = fracG * Inv16; // fractional G
            float fb = fracB * Inv16; // fractional B

            float wR0 = 1f - fr;
            float wR1 = fr;
            float wG0 = 1f - fg;
            float wG1 = fg;
            float wB0 = 1f - fb;
            float wB1 = fb;

            // Base (r,g,b) lattice point linear index in packed layout (R outer, G middle, B inner).
            int baseIndex = rBaseIndex * TripleStrideR + gBaseIndex * TripleStrideG + bBaseIndex; // (r0,g0,b0)

            // Fetch lattice colors.
            ref Vector3 c000 = ref lut[baseIndex]; // (r0,g0,b0)
            ref Vector3 c001 = ref Unsafe.Add(ref c000, 1); // i001
            ref Vector3 c100 = ref Unsafe.Add(ref c000, TripleStrideR); // (r1,g0,b0)
            ref Vector3 c101 = ref Unsafe.Add(ref c100, 1); // i101
            ref Vector3 c010 = ref Unsafe.Add(ref c000, TripleStrideG); // (r0,g1,b0)
            ref Vector3 c011 = ref Unsafe.Add(ref c010, 1); // i011
            ref Vector3 c110 = ref Unsafe.Add(ref c000, TripleStrideRG); // (r1,g1,b0)
            ref Vector3 c111 = ref Unsafe.Add(ref c110, 1); // i111

            // Compute trilinear interpolation.
            Vector3 accum = c000 * (wR0 * wG0 * wB0) + c100 * (wR1 * wG0 * wB0) + c010 * (wR0 * wG1 * wB0) + c110 * (wR1 * wG1 * wB0) + c001 * (wR0 * wG0 * wB1) + c101 * (wR1 * wG0 * wB1) + c011 * (wR0 * wG1 * wB1) + c111 * (wR1 * wG1 * wB1);

            destination->R = (byte)accum.X;
            destination->G = (byte)accum.Y;
            destination->B = (byte)accum.Z;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Rgba
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
    }
}
