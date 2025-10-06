using System;
using System.Runtime.CompilerServices;
using PdfReader.Rendering.Image.Jpg.Decoding;
using System.Numerics;

namespace PdfReader.Rendering.Image.Jpg.Idct
{
    /// <summary>
    /// 8x8 inverse DCT using libjpeg's canonical integer IDCT (jidctint / ISLOW).
    /// Fixed-point arithmetic with CONST_BITS = 13 and PASS1_BITS = 2.
    /// Input: 64 dequantized coefficients in natural (row-major) order.
    /// Output: 8x8 bytes with level shift (+128) and clamping to [0,255].
    /// </summary>
    internal static class JpgIdct
    {
        private const int DctSize = 8;
        private const int ConstBits = 13;
        private const int Pass1Bits = 2;
        private const int CenterSample = 128;

        // Fixed-point constants scaled by CONST_BITS (from IJG jidctint.c)
        private const int FIX_0_298631336 = 2446;   // FIX(0.298631336)
        private const int FIX_0_390180644 = 3196;   // FIX(0.390180644)
        private const int FIX_0_541196100 = 4433;   // FIX(0.541196100)
        private const int FIX_0_765366865 = 6270;   // FIX(0.765366865)
        private const int FIX_0_899976223 = 7373;   // FIX(0.899976223)
        private const int FIX_1_175875602 = 9633;   // FIX(1.175875602)
        private const int FIX_1_501321110 = 12299;  // FIX(1.501321110)
        private const int FIX_1_847759065 = 15137;  // FIX(1.847759065)
        private const int FIX_1_961570560 = 16069;  // FIX(1.961570560)
        private const int FIX_2_053119869 = 16819;  // FIX(2.053119869)
        private const int FIX_2_562915447 = 20995;  // FIX(2.562915447)
        private const int FIX_3_072711026 = 25172;  // FIX(3.072711026)

        public static void Transform(ReadOnlySpan<int> input, Span<byte> output, int outStride)
        {
            Span<int> workspace = stackalloc int[64];
            Transform(input, output, outStride, workspace);
        }

        /// <summary>
        /// Transform with caller-provided workspace to avoid per-call stack allocation.
        /// </summary>
        public static void Transform(ReadOnlySpan<int> input, Span<byte> output, int outStride, Span<int> workspace)
        {
            if (input.Length < DctSize * DctSize)
            {
                throw new ArgumentException("input must have 64 coefficients");
            }

            if (outStride <= 0 || output.Length < (7 * outStride + 8))
            {
                throw new ArgumentOutOfRangeException(nameof(output), "output buffer too small");
            }

            if (workspace.Length < 64)
            {
                throw new ArgumentException("workspace must be at least 64 ints", nameof(workspace));
            }

            // Pass 1: rows
            for (int row = 0; row < 64; row += 8)
            {
                IdctRow(workspace.Slice(row, 8), input.Slice(row, 8));
            }

            // Pass 2: columns and store
            for (int col = 0; col < 8; col++)
            {
                IdctColAndStore(workspace, col, output, outStride);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IdctRow(Span<int> outRow, ReadOnlySpan<int> inRow)
        {
            if ((inRow[1] | inRow[2] | inRow[3] | inRow[4] | inRow[5] | inRow[6] | inRow[7]) == 0)
            {
                int dc = inRow[0] << Pass1Bits;
                outRow[0] = dc;
                outRow[1] = dc;
                outRow[2] = dc;
                outRow[3] = dc;
                outRow[4] = dc;
                outRow[5] = dc;
                outRow[6] = dc;
                outRow[7] = dc;
                return;
            }

            int coefficient0 = inRow[0];
            int coefficient1 = inRow[1];
            int coefficient2 = inRow[2];
            int coefficient3 = inRow[3];
            int coefficient4 = inRow[4];
            int coefficient5 = inRow[5];
            int coefficient6 = inRow[6];
            int coefficient7 = inRow[7];

            // Even part
            long z2 = coefficient2;
            long z3 = coefficient6;
            long z1 = (z2 + z3) * FIX_0_541196100;
            long tmp2 = z1 - z3 * FIX_1_847759065;
            long tmp3 = z1 + z2 * FIX_0_765366865;

            long tmp0 = ((long)(coefficient0 + coefficient4)) << ConstBits;
            long tmp1 = ((long)(coefficient0 - coefficient4)) << ConstBits;

            long tmp10 = tmp0 + tmp3;
            long tmp13 = tmp0 - tmp3;
            long tmp11 = tmp1 + tmp2;
            long tmp12 = tmp1 - tmp2;

            // Odd part per IJG
            long t0 = coefficient7;
            long t1 = coefficient5;
            long t2 = coefficient3;
            long t3 = coefficient1;

            long oz2 = t0 + t2;
            long oz3 = t1 + t3;

            long oz1 = (oz2 + oz3) * FIX_1_175875602;
            oz2 = oz2 * -FIX_1_961570560;
            oz3 = oz3 * -FIX_0_390180644;
            oz2 += oz1;
            oz3 += oz1;

            long oz1b = (t0 + t3) * -FIX_0_899976223;
            long otmp0 = t0 * FIX_0_298631336 + oz1b + oz2;
            long otmp3 = t3 * FIX_1_501321110 + oz1b + oz3;

            long oz1c = (t1 + t2) * -FIX_2_562915447;
            long otmp1 = t1 * FIX_2_053119869 + oz1c + oz3;
            long otmp2 = t2 * FIX_3_072711026 + oz1c + oz2;

            outRow[0] = Descale(tmp10 + otmp3, ConstBits - Pass1Bits);
            outRow[7] = Descale(tmp10 - otmp3, ConstBits - Pass1Bits);
            outRow[1] = Descale(tmp11 + otmp2, ConstBits - Pass1Bits);
            outRow[6] = Descale(tmp11 - otmp2, ConstBits - Pass1Bits);
            outRow[2] = Descale(tmp12 + otmp1, ConstBits - Pass1Bits);
            outRow[5] = Descale(tmp12 - otmp1, ConstBits - Pass1Bits);
            outRow[3] = Descale(tmp13 + otmp0, ConstBits - Pass1Bits);
            outRow[4] = Descale(tmp13 - otmp0, ConstBits - Pass1Bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void IdctColAndStore(Span<int> workspace, int col, Span<byte> output, int outStride)
        {
            int c0 = workspace[col + 8 * 0];
            int c1 = workspace[col + 8 * 1];
            int c2 = workspace[col + 8 * 2];
            int c3 = workspace[col + 8 * 3];
            int c4 = workspace[col + 8 * 4];
            int c5 = workspace[col + 8 * 5];
            int c6 = workspace[col + 8 * 6];
            int c7 = workspace[col + 8 * 7];

            if ((c1 | c2 | c3 | c4 | c5 | c6 | c7) == 0)
            {
                int dc = Descale(c0, Pass1Bits + 3) + CenterSample;
                byte b = RangeLimit(dc);

                int outIndex = col;
                for (int y = 0; y < 8; y++)
                {
                    output[outIndex] = b;
                    outIndex += outStride;
                }
                return;
            }

            c0 += 1 << (Pass1Bits - 1);

            long z2 = c2;
            long z3 = c6;
            long z1 = (z2 + z3) * FIX_0_541196100;
            long tmp2 = z1 - z3 * FIX_1_847759065;
            long tmp3 = z1 + z2 * FIX_0_765366865;

            long tmp0 = ((long)(c0 + c4)) << ConstBits;
            long tmp1 = ((long)(c0 - c4)) << ConstBits;

            long tmp10 = tmp0 + tmp3;
            long tmp13 = tmp0 - tmp3;
            long tmp11 = tmp1 + tmp2;
            long tmp12 = tmp1 - tmp2;

            // Odd part per IJG
            long t0 = c7;
            long t1 = c5;
            long t2 = c3;
            long t3 = c1;

            long oz2 = t0 + t2;
            long oz3 = t1 + t3;
            long oz1 = (oz2 + oz3) * FIX_1_175875602;
            oz2 = oz2 * -FIX_1_961570560;
            oz3 = oz3 * -FIX_0_390180644;
            oz2 += oz1;
            oz3 += oz1;

            long oz1b = (t0 + t3) * -FIX_0_899976223;
            long otmp0 = t0 * FIX_0_298631336 + oz1b + oz2;
            long otmp3 = t3 * FIX_1_501321110 + oz1b + oz3;

            long oz1c = (t1 + t2) * -FIX_2_562915447;
            long otmp1 = t1 * FIX_2_053119869 + oz1c + oz3;
            long otmp2 = t2 * FIX_3_072711026 + oz1c + oz2;

            int shift = ConstBits + Pass1Bits + 3;
            int out0 = Descale(tmp10 + otmp3, shift) + CenterSample;
            int out1 = Descale(tmp11 + otmp2, shift) + CenterSample;
            int out2 = Descale(tmp12 + otmp1, shift) + CenterSample;
            int out3 = Descale(tmp13 + otmp0, shift) + CenterSample;
            int out4 = Descale(tmp13 - otmp0, shift) + CenterSample;
            int out5 = Descale(tmp12 - otmp1, shift) + CenterSample;
            int out6 = Descale(tmp11 - otmp2, shift) + CenterSample;
            int out7 = Descale(tmp10 - otmp3, shift) + CenterSample;

            int baseIndex = col;
            output[baseIndex + 0 * outStride] = RangeLimit(out0);
            output[baseIndex + 1 * outStride] = RangeLimit(out1);
            output[baseIndex + 2 * outStride] = RangeLimit(out2);
            output[baseIndex + 3 * outStride] = RangeLimit(out3);
            output[baseIndex + 4 * outStride] = RangeLimit(out4);
            output[baseIndex + 5 * outStride] = RangeLimit(out5);
            output[baseIndex + 6 * outStride] = RangeLimit(out6);
            output[baseIndex + 7 * outStride] = RangeLimit(out7);
        }

        /// <summary>
        /// Transform using a precomputed ScaledIdctPlan with zig-zag input. Fuses dequantization and zig-zag remap.
        /// </summary>
        public static void TransformScaledZigZag(int[] inputZigZag, ScaledIdctPlan plan, Span<byte> output, int outStride, int[] workspace, int[] subWorkspace)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (inputZigZag.Length < 64)
            {
                throw new ArgumentException("input must have 64 coefficients");
            }

            int vec = Vector<int>.Count;
            int i = 0;
            for (; i <= 64 - vec; i += vec)
            {
                var vi = new Vector<int>(inputZigZag, i);
                var vq = new Vector<int>(plan.DequantZig, i);
                (vi * vq).CopyTo(subWorkspace, i);
            }
            for (; i < 64; i++)
            {
                subWorkspace[i] = inputZigZag[i] * plan.DequantZig[i];
            }
            for (i = 0; i < 64; i++)
            {
                int natural = JpgZigZag.Table[i];
                workspace[natural] = subWorkspace[i];
            }

            Transform(workspace, output, outStride, workspace);
        }

        /// <summary>
        /// Transform using a precomputed ScaledIdctPlan with natural-order input (dequant multipliers in natural order).
        /// </summary>
        public static void TransformScaledNatural(int[] inputNatural, ScaledIdctPlan plan, Span<byte> output, int outStride, int[] workspace)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (inputNatural.Length < 64)
            {
                throw new ArgumentException("input must have 64 coefficients");
            }

            // Multiply by dequant (natural order) into workspace then run standard transform
            int vec = Vector<int>.Count;
            int i = 0;
            for (; i <= 64 - vec; i += vec)
            {
                var vi = new Vector<int>(inputNatural, i);
                var vq = new Vector<int>(plan.DequantNatural, i);
                (vi * vq).CopyTo(workspace, i);
            }
            for (; i < 64; i++)
            {
                workspace[i] = inputNatural[i] * plan.DequantNatural[i];
            }

            Transform(workspace, output, outStride, workspace);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Descale(long value, int n)
        {
            // JPEG-style RIGHT_SHIFT with round-to-nearest behavior matching libjpeg.
            // For negative values, use (bias-1) to preserve symmetry.
            long bias = 1L << (n - 1);
            if (value >= 0)
            {
                return (int)((value + bias) >> n);
            }
            else
            {
                return (int)((value + bias - 1) >> n);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte RangeLimit(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 255)
            {
                return 255;
            }

            return (byte)value;
        }
    }
}
