using PdfReader.Models;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Color.Clut
{
    /// <summary>
    /// Helper for building and sampling 3D device->sRGB lookup tables.
    /// LUT layout: contiguous packed RGB triples for each lattice point in (R,G,B) iteration order.
    /// Bilinear sampling (RG with nearest B) uses precomputed weight quads that sum to 1 (normalized floats).
    /// TODO: we need a separate version of this converter that specifically handles Gray (3 identical components)->sRGB.
    /// </summary>
    internal sealed class TreeDLut : IRgbaSampler
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

        private readonly Vector3[] _lut;

        private TreeDLut(Vector3[] lut)
        {
            _lut = lut;
        }

        /// <summary>
        /// Builds a packed Vector3 (device to sRGB) sampled uniformly over each dimension.
        /// The LUT layout order is R (outer), G (middle), B (inner) for memory locality.
        /// Each Vector3 is stored as (R, G, B) mapping directly to pixel components.
        /// </summary>
        /// <param name="intent">The rendering intent controlling device to sRGB conversion.</param>
        /// <param name="converter">Delegate converting normalized device color to sRGB SKColor.</param>
        /// <returns>A new <see cref="TreeDLut"/> instance containing the sampled LUT, or null if converter is null.</returns>
        public static TreeDLut Build(PdfRenderingIntent intent, DeviceToSrgbCore converter)
        {
            if (converter == null)
            {
                return default;
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
            return new TreeDLut(lut);
        }

        /// <summary>
        /// False as this converter modifies color.
        /// </summary>
        public bool IsDefault => false;

        /// <summary>
        /// Performs trilinear sampling of the LUT for a single pixel.
        /// The source pixel's R, G, and B values are used to sample the LUT.
        /// The result is written to the destination pixel.
        /// </summary>
        /// <param name="source">The source pixel to sample (R, G, B components).</param>
        /// <param name="destination">The destination pixel to receive the sampled color.</param>
        public void Sample(ref Rgba source, ref Rgba destination)
        {
            Sample(ref _lut[0], ref source, ref destination);
        }

        /// <summary>
        /// Performs trilinear interpolation over R, G, B using the provided LUT.
        /// The LUT must be a packed array of Vector3 values in (R, G, B) order.
        /// The source pixel's R, G, and B values are used to sample the LUT.
        /// The result is written to the destination pixel.
        /// </summary>
        /// <param name="lut">Reference to the first element of the LUT array.</param>
        /// <param name="source">The source pixel to sample (R, G, B components).</param>
        /// <param name="destination">The destination pixel to receive the sampled color.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sample(ref Vector3 lut, ref Rgba source, ref Rgba destination)
        {
            var r = source.R;
            var g = source.G;
            var b = source.B;

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
            ref Vector3 c000 = ref Unsafe.Add(ref lut, baseIndex); // (r0,g0,b0)
            ref Vector3 c001 = ref Unsafe.Add(ref c000, 1); // i001
            ref Vector3 c100 = ref Unsafe.Add(ref c000, TripleStrideR); // (r1,g0,b0)
            ref Vector3 c101 = ref Unsafe.Add(ref c100, 1); // i101
            ref Vector3 c010 = ref Unsafe.Add(ref c000, TripleStrideG); // (r0,g1,b0)
            ref Vector3 c011 = ref Unsafe.Add(ref c010, 1); // i011
            ref Vector3 c110 = ref Unsafe.Add(ref c000, TripleStrideRG); // (r1,g1,b0)
            ref Vector3 c111 = ref Unsafe.Add(ref c110, 1); // i111

            // Compute trilinear interpolation.
            Vector3 accum = c000 * (wR0 * wG0 * wB0) + c100 * (wR1 * wG0 * wB0) + c010 * (wR0 * wG1 * wB0) + c110 * (wR1 * wG1 * wB0) + c001 * (wR0 * wG0 * wB1) + c101 * (wR1 * wG0 * wB1) + c011 * (wR0 * wG1 * wB1) + c111 * (wR1 * wG1 * wB1);

            destination.R = (byte)accum.X;
            destination.G = (byte)accum.Y;
            destination.B = (byte)accum.Z;
        }
    }
}
