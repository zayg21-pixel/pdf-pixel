using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PdfReader.Models;
using SkiaSharp;

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

        /// <summary>
        /// Weight lookup for all (fracR, fracG) pairs (16 x 16 = 256 entries).
        /// Each entry stores four normalized corner weights (sum == 1).
        /// </summary>
        private static readonly WeightQuad[] WeightTable = BuildWeightTable();

        /// <summary>
        /// Holds the four bilinear weights for a (fracR, fracG) pair (normalized floats).
        /// Provides Vector4 and (when available) Vector128<float> representations for SIMD dot products.
        /// </summary>
        private readonly struct WeightQuad
        {
            public Vector4 VectorQuad { get; } // (w00, w10, w01, w11) sum == 1

            public WeightQuad(ushort w00, ushort w10, ushort w01, ushort w11)
            {
                float invScale = 1f / WeightScale;
                float fw00 = w00 * invScale;
                float fw10 = w10 * invScale;
                float fw01 = w01 * invScale;
                float fw11 = 1f - (fw00 + fw10 + fw01); // enforce exact sum=1
                VectorQuad = new Vector4(fw00, fw10, fw01, fw11);
            }
        }

        /// <summary>
        /// Builds the static weight table once at startup using fixed-point math then normalizes to floats.
        /// </summary>
        private static WeightQuad[] BuildWeightTable()
        {
            var table = new WeightQuad[16 * 16];
            for (int fracR = 0; fracR < 16; fracR++)
            {
                int oneMinusFracR = FractionMax - fracR; // 16 - fracR
                for (int fracG = 0; fracG < 16; fracG++)
                {
                    int oneMinusFracG = FractionMax - fracG; // 16 - fracG
                    int w00 = oneMinusFracR * oneMinusFracG;
                    int w10 = fracR * oneMinusFracG;
                    int w01 = oneMinusFracR * fracG;
                    int w11 = WeightScale - (w00 + w10 + w01); // Maintain invariant sum = 256
                    int index = (fracR << 4) | fracG;
                    table[index] = new WeightQuad((ushort)w00, (ushort)w10, (ushort)w01, (ushort)w11);
                }
            }
            return table;
        }

        /// <summary>
        /// Builds a packed RGB 3D LUT (device -> sRGB) sampled uniformly over each dimension.
        /// Layout order: R (outer), G (middle), B (inner).
        /// </summary>
        internal static unsafe byte[] Build8Bit(PdfRenderingIntent intent, DeviceToSrgbCore converter)
        {
            if (converter == null)
            {
                return null;
            }
            int n = GridSize;
            int totalPoints = n * n * n;
            int totalBytes = totalPoints * sizeof(Rgb); // sizeof(Rgb) == 3 (Pack=1)
            byte[] lut = new byte[totalBytes];
            fixed (byte* lutPtr = lut)
            {
                Rgb* rgbPtr = (Rgb*)lutPtr;
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
                            rgbPtr[writeIndex].R = color.Red;
                            rgbPtr[writeIndex].G = color.Green;
                            rgbPtr[writeIndex].B = color.Blue;
                            writeIndex++;
                        }
                    }
                }
            }
            return lut;
        }

        /// <summary>
        /// Shared core for bilinear RG + nearest B sampling. Writes all three RGB components via single struct assignment.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void SampleBilinearCore(Rgb* rgbLut, int rIndexBase, int gIndexBase, int bSlice, ref WeightQuad weights, Rgb* destination)
        {
            // Compute lattice indices for four RG corners at chosen B slice.
            int baseTriple = rIndexBase * TripleStrideR + gIndexBase * TripleStrideG + bSlice;
            int i00 = baseTriple;
            int i10 = i00 + TripleStrideR;
            int i01 = i00 + TripleStrideG;
            int i11 = i00 + TripleStrideR + TripleStrideG;

            // Gather corner samples.
            Rgb c00 = rgbLut[i00];
            Rgb c10 = rgbLut[i10];
            Rgb c01 = rgbLut[i01];
            Rgb c11 = rgbLut[i11];

            Vector4 wv = weights.VectorQuad;
            // Build channel vectors and compute dot products.
            float rVal = Vector4.Dot(new Vector4(c00.R, c10.R, c01.R, c11.R), wv);
            float gVal = Vector4.Dot(new Vector4(c00.G, c10.G, c01.G, c11.G), wv);
            float bVal = Vector4.Dot(new Vector4(c00.B, c10.B, c01.B, c11.B), wv);

            // Single struct write (truncate toward zero). For rounding add +0.5f to each component before cast if desired.
            Rgb outPixel;
            outPixel.R = (byte)rVal;
            outPixel.G = (byte)gVal;
            outPixel.B = (byte)bVal;
            *destination = outPixel;
        }

        /// <summary>
        /// Bilinear sampling over R,G with nearest B. Normalized weights eliminate explicit normalization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SampleBilinear8(byte[] lut, byte rByte, byte gByte, byte bByte, byte* destination)
        {
            int rIndexBase = rByte >> GridIndexShift;
            int gIndexBase = gByte >> GridIndexShift;
            int bIndexBase = bByte >> GridIndexShift;
            int fracR = rByte & GridIndexMask;
            int fracG = gByte & GridIndexMask;
            int fracB = bByte & GridIndexMask;
            int bSlice = fracB < 8 ? bIndexBase : (bIndexBase + 1);
            int weightIndex = (fracR << 4) | fracG;
            ref WeightQuad w = ref WeightTable[weightIndex];
            fixed (byte* lutPtr = lut)
            {
                SampleBilinearCore((Rgb*)lutPtr, rIndexBase, gIndexBase, bSlice, ref w, (Rgb*)destination);
            }
        }

        /// <summary>
        /// In-place bilinear sampling for an RGBA row. RGB updated, Alpha preserved.
        /// </summary>
        internal static unsafe void SampleBilinear8RgbaInPlace(byte* lutPtr, byte* rgbaRowPtr, int pixelCount)
        {
            if (lutPtr == null)
            {
                return; // LUT not available
            }
            if (rgbaRowPtr == null)
            {
                return; // Destination buffer null
            }
            if (pixelCount <= 0)
            {
                return; // Nothing to process
            }
            Rgb* rgbLut = (Rgb*)lutPtr;
            Rgba* pixels = (Rgba*)rgbaRowPtr;
            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                Rgba* pixelPtr = pixels + pixelIndex;
                int rIndexBase = pixelPtr->R >> GridIndexShift;
                int gIndexBase = pixelPtr->G >> GridIndexShift;
                int bIndexBase = pixelPtr->B >> GridIndexShift;
                int fracR = pixelPtr->R & GridIndexMask;
                int fracG = pixelPtr->G & GridIndexMask;
                int fracB = pixelPtr->B & GridIndexMask;
                int bSlice = fracB < 8 ? bIndexBase : (bIndexBase + 1);
                int weightIndex = (fracR << 4) | fracG;
                ref WeightQuad w = ref WeightTable[weightIndex];
                SampleBilinearCore(rgbLut, rIndexBase, gIndexBase, bSlice, ref w, (Rgb*)pixelPtr); // writes R,G,B only
                // Alpha left unchanged. TODO: migh not be usefull here.
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Rgb
        {
            public byte R;
            public byte G;
            public byte B;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Rgba // TODO: migh not be usefull here.
        {
            public byte R;
            public byte G;
            public byte B;
            public byte A;
        }
    }
}
