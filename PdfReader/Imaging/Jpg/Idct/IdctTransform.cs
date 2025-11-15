using PdfReader.Imaging.Jpg.Model;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Jpg.Idct;

/// <summary>
/// Performs inverse discrete cosine transform (IDCT) operations over 8x8 image blocks using an AAN scaled algorithm.
/// The implementation works on a packed <see cref="Block8x8F"/> where each logical row is split into two <see cref="Vector4"/> halves.
/// </summary>
internal static partial class IdctTransform
{
    private const int DctSize = 8;
    private const int CenterSample = 128;

    // AAN / DCT related constants (vector broadcast). Explanatory comments reference common cosine identities.
    private static readonly Vector4 C_1_414213562 = new(1.414213562f); // sqrt(2)
    private static readonly Vector4 C_1_847759065 = new(1.847759065f); // sqrt(2) * cos(pi/8)
    private static readonly Vector4 C_N1_082392200 = new(-1.082392200f); // -sqrt(2) * cos(3pi/8)
    private static readonly Vector4 C_N2_613125930 = new(-2.613125930f); // -sqrt(2) * (cos(pi/8) + cos(3pi/8))

    // Unused in the current variant but retained (with clarification) in case other partials / future optimizations rely on them:
    private static readonly Vector4 C_0_707106781 = new(0.707106781f); // 1 / sqrt(2)
    private static readonly Vector4 C_0_382683433 = new(0.382683433f); // sin(pi/8)
    private static readonly Vector4 C_0_541196100 = new(0.541196100f); // sqrt(2) * cos(3pi/8)
    private static readonly Vector4 C_1_306562965 = new(1.306562965f); // cos(pi/16) + cos(3pi/16)

    private const float LevelShift = 128f;
    private static readonly Vector4 LevelShiftVector = new(LevelShift);

    private static readonly Block8x8F AanInputScaleBlock = BuildAanInputScaleBlock();

    /// <summary>
    /// Applies de-quantization (if not DC-only) and full IDCT (AAN scaled) to a block in natural order.
    /// </summary>
    /// <param name="inputNatural">The source block (in-place transformed).</param>
    /// <param name="dequantBlock">Precomputed dequantization block.</param>
    /// <param name="dcOnly">True to process only the DC coefficient (fast path).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TransformScaledNatural(ref Block8x8F inputNatural, ref Block8x8F dequantBlock, bool dcOnly)
    {
        if (dcOnly)
        {
            FillBlockFromDc(ref inputNatural, inputNatural[0] * dequantBlock[0]);
            return;
        }

        // In-place de-quantization (vectorized lanes per 8x4 panel)
        inputNatural.MultiplyBy(dequantBlock);

        ApplyTransform(ref inputNatural);
    }

    /// <summary>
    /// Fills an entire block from a de-quantized DC coefficient (all AC terms are zero in this path).
    /// </summary>
    /// <param name="inputNatural">Block to fill.</param>
    /// <param name="dcDequant">De-quantized DC coefficient.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillBlockFromDc(ref Block8x8F inputNatural, float dcDequant)
    {
        float pixel = dcDequant / DctSize + CenterSample;
        inputNatural.Clear(pixel);
    }

    /// <summary>
    /// Performs AAN input scaling, a two-pass (row/column) IDCT, and final level shift.
    /// </summary>
    /// <param name="block">Block to transform (in-place).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyTransform(ref Block8x8F block)
    {
        block.MultiplyBy(AanInputScaleBlock);
        block.Transpose();
        PerformTwoPassIdct(ref block);
        block.Add(LevelShiftVector);
    }

    /// <summary>
    /// Executes column then row 1-D IDCT passes (after an initial transpose) using a 8x4 Vector4 layout.
    /// </summary>
    /// <param name="transposedBlock">Block already transposed for column processing.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PerformTwoPassIdct(ref Block8x8F transposedBlock)
    {
        Idct8x4InPlace(ref transposedBlock.Row0Left);
        Idct8x4InPlace(ref transposedBlock.Row0Right);
        transposedBlock.Transpose();
        Idct8x4InPlace(ref transposedBlock.Row0Left);
        Idct8x4InPlace(ref transposedBlock.Row0Right);
    }

    /// <summary>
    /// In-place 1-D IDCT over 8 samples distributed across four Vector4 registers (even indices first, then odd).
    /// Variable naming distinguishes the even part (E*) from the odd part (O*) of the butterfly for readability.
    /// </summary>
    /// <param name="vecRef">Reference to the first Vector4 of the 8x4 panel.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Idct8x4InPlace(ref Vector4 vecRef)
    {
        // Even part (indices 0,2,4,6)
        Vector4 even0 = Unsafe.Add(ref vecRef, 0 * 2);
        Vector4 even1 = Unsafe.Add(ref vecRef, 2 * 2);
        Vector4 even2 = Unsafe.Add(ref vecRef, 4 * 2);
        Vector4 even3 = Unsafe.Add(ref vecRef, 6 * 2);

        Vector4 evenSum02 = even0 + even2;
        Vector4 evenDiff02 = even0 - even2;
        Vector4 evenSum13 = even1 + even3;
        Vector4 evenTmp12 = (even1 - even3) * C_1_414213562 - evenSum13;

        Vector4 out0Even = evenSum02 + evenSum13;
        Vector4 out3Even = evenSum02 - evenSum13;
        Vector4 out1Even = evenDiff02 + evenTmp12;
        Vector4 out2Even = evenDiff02 - evenTmp12;

        // Odd part (indices 1,3,5,7)
        Vector4 odd0 = Unsafe.Add(ref vecRef, 1 * 2);
        Vector4 odd1 = Unsafe.Add(ref vecRef, 3 * 2);
        Vector4 odd2 = Unsafe.Add(ref vecRef, 5 * 2);
        Vector4 odd3 = Unsafe.Add(ref vecRef, 7 * 2);

        Vector4 sumOdd2Odd1 = odd2 + odd1;
        Vector4 diffOdd2Odd1 = odd2 - odd1;
        Vector4 sumOdd0Odd3 = odd0 + odd3;
        Vector4 diffOdd0Odd3 = odd0 - odd3;

        Vector4 out7OddBase = sumOdd0Odd3 + sumOdd2Odd1;
        Vector4 oddTmp11 = (sumOdd0Odd3 - sumOdd2Odd1) * C_1_414213562;
        Vector4 oddIntermediate = (diffOdd2Odd1 + diffOdd0Odd3) * C_1_847759065;
        Vector4 oddTmp10 = diffOdd0Odd3 * C_N1_082392200 + oddIntermediate;
        Vector4 oddTmp12 = diffOdd2Odd1 * C_N2_613125930 + oddIntermediate;
        Vector4 out6Odd = oddTmp12 - out7OddBase;
        Vector4 out5Odd = oddTmp11 - out6Odd;
        Vector4 out4Odd = oddTmp10 - out5Odd;

        // Store results (butterfly combination of even/odd parts)
        Unsafe.Add(ref vecRef, 0 * 2) = out0Even + out7OddBase;
        Unsafe.Add(ref vecRef, 7 * 2) = out0Even - out7OddBase;
        Unsafe.Add(ref vecRef, 1 * 2) = out1Even + out6Odd;
        Unsafe.Add(ref vecRef, 6 * 2) = out1Even - out6Odd;
        Unsafe.Add(ref vecRef, 2 * 2) = out2Even + out5Odd;
        Unsafe.Add(ref vecRef, 5 * 2) = out2Even - out5Odd;
        Unsafe.Add(ref vecRef, 3 * 2) = out3Even + out4Odd;
        Unsafe.Add(ref vecRef, 4 * 2) = out3Even - out4Odd;
    }

    /// <summary>
    /// Builds the AAN input scaling block (pre-multipliers applied before the two-pass IDCT).
    /// </summary>
    /// <returns>Initialized scaling block.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Block8x8F BuildAanInputScaleBlock()
    {
        Block8x8F scaleBlock = default;
        Span<float> scaleFactors = stackalloc float[DctSize];
        scaleFactors[0] = 1f;
        for (int k = 1; k < DctSize; k++)
        {
            scaleFactors[k] = MathF.Cos(k * MathF.PI / 16f) * MathF.Sqrt(2f);
        }

        int linearIndex = 0;
        for (int rowIndex = 0; rowIndex < DctSize; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < DctSize; columnIndex++)
            {
                float factor = 0.125f * scaleFactors[rowIndex] * scaleFactors[columnIndex];
                scaleBlock[linearIndex] = factor;
                linearIndex++;
            }
        }

        return scaleBlock;
    }
}
