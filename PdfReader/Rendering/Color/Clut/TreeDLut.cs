using System;
using System.Runtime.CompilerServices;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReader.Rendering.Color.Clut
{
    internal delegate SKColor DeviceToSrgbCore(ReadOnlySpan<float> input, PdfRenderingIntent intent);

    /// <summary>
    /// Helper for building and sampling 3D device->sRGB lookup tables.
    /// Provides bilinear (RG + nearest B) and full trilinear interpolation methods.
    /// </summary>
    internal static class TreeDLut
    {
        internal const int GridSize = 17; // 17^3 = 4913 lattice points
        private const int GridIndexShift = 4; // 4 bits for index (0..15), 4 bits for fraction (0..15)
        private const int GridIndexMask = 0x0F; // fractional mask
        private const int FractionMax = 16; // normalized fraction range (0..16)
        private const int WeightScale = 256; // bilinear weight sum normalization

        // Strides for index math.
        private const int StrideB = 3; // 3 bytes per lattice point
        private const int StrideG = GridSize * StrideB; // bytes to advance one G index
        private const int StrideR = GridSize * StrideG; // bytes to advance one R index

        /// <summary>
        /// Weight lookup for all (fracR, fracG) pairs (16 x 16 = 256 entries).
        /// Each entry stores the four bilinear weights (w00, w10, w01, w11) that sum to 256.
        /// Eliminates three multiplications and one subtraction per pixel in the hot loop.
        /// </summary>
        private static readonly WeightQuad[] WeightTable = BuildWeightTable();

        /// <summary>
        /// Holds the four bilinear weights for a (fracR, fracG) pair.
        /// Stored as 16-bit values (range 0..256). Marked readonly for immutability.
        /// </summary>
        private readonly struct WeightQuad
        {
            public readonly ushort W00; // (1-dr)(1-dg)
            public readonly ushort W10; // dr(1-dg)
            public readonly ushort W01; // (1-dr)dg
            public readonly ushort W11; // dr dg

            public WeightQuad(ushort w00, ushort w10, ushort w01, ushort w11)
            {
                W00 = w00;
                W10 = w10;
                W01 = w01;
                W11 = w11;
            }
        }

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

        internal static byte[] Build8Bit(PdfRenderingIntent intent, DeviceToSrgbCore converter)
        {
            if (converter == null)
            {
                return null;
            }

            int n = GridSize;
            int totalPoints = n * n * n;
            int totalBytes = totalPoints * 3;
            byte[] lut = new byte[totalBytes];
            int writeIndex = 0;

            for (int rIndex = 0; rIndex < n; rIndex++)
            {
                float r = (float)rIndex / (n - 1);
                for (int gIndex = 0; gIndex < n; gIndex++)
                {
                    float g = (float)gIndex / (n - 1);
                    for (int bIndex = 0; bIndex < n; bIndex++)
                    {
                        float b = (float)bIndex / (n - 1);
                        ReadOnlySpan<float> input = stackalloc float[] { r, g, b };
                        SKColor color = converter(input, intent);
                        lut[writeIndex++] = color.Red;
                        lut[writeIndex++] = color.Green;
                        lut[writeIndex++] = color.Blue;
                    }
                }
            }

            return lut;
        }

        /// <summary>
        /// 8-bit bilinear sampling helper using fixed-point arithmetic for weights and accumulation (8-bit input, 17 grid points, power-of-two normalization).
        /// Performs on-the-fly stride computation (r0 * StrideR + g0 * StrideG) instead of lookup tables.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SampleBilinear8(
            byte[] lut,
            byte r,
            byte g,
            byte b,
            byte* destination)
        {
            const int HalfWeight = WeightScale / 2; // 128

            int r0 = r >> GridIndexShift; // 0..15
            int g0 = g >> GridIndexShift; // 0..15
            int b0 = b >> GridIndexShift; // 0..15

            int fracR = r & GridIndexMask; // 0..15
            int fracG = g & GridIndexMask; // 0..15
            int fracB = b & GridIndexMask; // 0..15

            int bSlice = fracB < 8 ? b0 : (b0 + 1); // 0..16

            // Base offset for (r0,g0) at chosen B slice using direct multiplication.
            int baseRG0 = r0 * StrideR + g0 * StrideG;
            int sliceOffset = bSlice * StrideB;

            int baseR0G0 = baseRG0 + sliceOffset;
            int baseR1G0 = baseR0G0 + StrideR;          // r1 == r0 + 1
            int baseR0G1 = baseR0G0 + StrideG;          // g1 == g0 + 1
            int baseR1G1 = baseR0G0 + StrideR + StrideG; // r1,g1

            int weightIndex = (fracR << 4) | fracG;
            ref WeightQuad w = ref WeightTable[weightIndex];

            int sumR = lut[baseR0G0] * w.W00 + lut[baseR1G0] * w.W10 + lut[baseR0G1] * w.W01 + lut[baseR1G1] * w.W11;
            int sumG = lut[baseR0G0 + 1] * w.W00 + lut[baseR1G0 + 1] * w.W10 + lut[baseR0G1 + 1] * w.W01 + lut[baseR1G1 + 1] * w.W11;
            int sumB = lut[baseR0G0 + 2] * w.W00 + lut[baseR1G0 + 2] * w.W10 + lut[baseR0G1 + 2] * w.W01 + lut[baseR1G1 + 2] * w.W11;

            destination[0] = (byte)((sumR + HalfWeight) >> 8);
            destination[1] = (byte)((sumG + HalfWeight) >> 8);
            destination[2] = (byte)((sumB + HalfWeight) >> 8);
        }

        /// <summary>
        /// In-place bilinear sampling for an RGBA row (stride 4). Only RGB bytes are updated; alpha bytes remain unchanged.
        /// Performs on-the-fly stride computation (r0 * StrideR + g0 * StrideG) instead of lookup tables.
        /// </summary>
        internal static unsafe void SampleBilinear8RgbaInPlace(byte* lut, byte* rgbaRow, int pixelCount)
        {
            const int HalfWeight = WeightScale / 2; // 128
            int pixelOffset = 0;

            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                byte rValue = rgbaRow[pixelOffset];
                byte gValue = rgbaRow[pixelOffset + 1];
                byte bValue = rgbaRow[pixelOffset + 2];

                int r0 = rValue >> GridIndexShift; // 0..15
                int g0 = gValue >> GridIndexShift; // 0..15
                int b0 = bValue >> GridIndexShift; // 0..15

                int fracR = rValue & GridIndexMask; // 0..15
                int fracG = gValue & GridIndexMask; // 0..15
                int fracB = bValue & GridIndexMask; // 0..15

                int bSlice = fracB < 8 ? b0 : (b0 + 1); // 0..16

                int baseRG0 = r0 * StrideR + g0 * StrideG;
                int sliceOffset = bSlice * StrideB;

                int baseR0G0 = baseRG0 + sliceOffset;
                int baseR1G0 = baseR0G0 + StrideR;
                int baseR0G1 = baseR0G0 + StrideG;
                int baseR1G1 = baseR0G0 + StrideR + StrideG;

                int weightIndex = (fracR << 4) | fracG;
                ref WeightQuad w = ref WeightTable[weightIndex];

                int sumR = lut[baseR0G0] * w.W00 + lut[baseR1G0] * w.W10 + lut[baseR0G1] * w.W01 + lut[baseR1G1] * w.W11;
                int sumG = lut[baseR0G0 + 1] * w.W00 + lut[baseR1G0 + 1] * w.W10 + lut[baseR0G1 + 1] * w.W01 + lut[baseR1G1 + 1] * w.W11;
                int sumB = lut[baseR0G0 + 2] * w.W00 + lut[baseR1G0 + 2] * w.W10 + lut[baseR0G1 + 2] * w.W01 + lut[baseR1G1 + 2] * w.W11;

                int rOut = (sumR + HalfWeight) >> 8;
                int gOut = (sumG + HalfWeight) >> 8;
                int bOut = (sumB + HalfWeight) >> 8;

                rgbaRow[pixelOffset] = (byte)rOut;
                rgbaRow[pixelOffset + 1] = (byte)gOut;
                rgbaRow[pixelOffset + 2] = (byte)bOut;

                pixelOffset += 4;
            }
        }
    }
}
