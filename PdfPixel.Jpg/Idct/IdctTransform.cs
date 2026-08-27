using PdfPixel.Jpg.Model;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jpg.Idct;

/// <summary>
/// Performs inverse discrete cosine transform (IDCT) operations over 8x8 image blocks using an AAN scaled algorithm.
/// The implementation works on a packed <see cref="Block8x8F"/> where each logical row is split into two <see cref="Vector4"/> halves.
/// A block can also be reconstructed at 4, 2 or 1 samples per dimension from the matching low-frequency coefficients;
/// the two dimensions are transformed by separate passes, so their sizes are independent.
/// </summary>
internal static class IdctTransform
{
    private const int DctSize = 8;
    private const int CenterSample = 128;

    // AAN / DCT related constants (vector broadcast). Explanatory comments reference common cosine identities.
    private static readonly Vector4 C_1_414213562 = new(1.414213562f); // sqrt(2)
    private static readonly Vector4 C_1_847759065 = new(1.847759065f); // sqrt(2) * cos(pi/8)
    private static readonly Vector4 C_N1_082392200 = new(-1.082392200f); // -sqrt(2) * cos(3pi/8)
    private static readonly Vector4 C_N2_613125930 = new(-2.613125930f); // -sqrt(2) * (cos(pi/8) + cos(3pi/8))
    private static readonly Vector4 C_1_306562965 = new(1.306562965f); // sqrt(2) * cos(pi/8), four-point odd part
    private static readonly Vector4 C_0_541196100 = new(0.541196100f); // sqrt(2) * cos(3pi/8), four-point odd part

    private const float LevelShift = 128f;

    // Share of the 1/8 normalization each of the two passes contributes.
    private const float PassScale = 0.35355339f; // 1 / sqrt(8)

    private static readonly Vector4 LevelShiftVector = new(LevelShift);

    private static readonly Block8x8F AanInputScaleBlock = BuildAanInputScaleBlock();

    // Input scaling for the reduced transforms, one block per (height, width) pair of transform sizes,
    // indexed by ScaleBlockIndex. Only a dimension the 8-point AAN kernel transforms carries AAN factors.
    private static readonly Block8x8F[] ReducedInputScaleBlocks = BuildReducedInputScaleBlocks();

    /// <summary>
    /// Applies de-quantization (if not DC-only) and IDCT to a block in natural order, reconstructing
    /// <paramref name="idctWidth"/> × <paramref name="idctHeight"/> samples into the block's upper-left corner.
    /// </summary>
    /// <param name="inputNatural">The source block (in-place transformed).</param>
    /// <param name="dequantBlock">Precomputed dequantization block.</param>
    /// <param name="dcOnly">True to process only the DC coefficient (fast path).</param>
    /// <param name="idctWidth">Reconstructed sample count per row (1, 2, 4 or 8).</param>
    /// <param name="idctHeight">Reconstructed sample count per column (1, 2, 4 or 8).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TransformScaledNatural(ref Block8x8F inputNatural, ref Block8x8F dequantBlock, bool dcOnly, int idctWidth, int idctHeight)
    {
        if (dcOnly)
        {
            FillBlockFromDc(ref inputNatural, inputNatural[0] * dequantBlock[0]);
            return;
        }

        if (idctWidth != DctSize || idctHeight != DctSize)
        {
            ApplyReducedTransform(ref inputNatural, ref dequantBlock, idctWidth, idctHeight);
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
        float pixel = (dcDequant / DctSize) + CenterSample;
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
    /// Scales the coefficients the requested sizes read, runs the two 1-D passes over them, and level
    /// shifts the reconstructed samples. Everything outside the reconstructed corner is left untouched.
    /// </summary>
    /// <param name="block">Block to transform (in-place).</param>
    /// <param name="dequantBlock">Precomputed dequantization block.</param>
    /// <param name="idctWidth">Reconstructed sample count per row.</param>
    /// <param name="idctHeight">Reconstructed sample count per column.</param>
    private static void ApplyReducedTransform(ref Block8x8F block, ref Block8x8F dequantBlock, int idctWidth, int idctHeight)
    {
        ref Block8x8F inputScaleBlock = ref ReducedInputScaleBlocks[ScaleBlockIndex(idctWidth, idctHeight)];
        ScaleCorner(ref block, ref dequantBlock, ref inputScaleBlock, idctWidth, idctHeight);

        int cornerSize = (idctWidth > idctHeight) ? idctWidth : idctHeight;
        TransposeCorner(ref block, cornerSize);
        IdctColumns(ref block, idctWidth, idctHeight);
        TransposeCorner(ref block, cornerSize);
        IdctColumns(ref block, idctHeight, idctWidth);

        LevelShiftCorner(ref block, idctWidth, idctHeight);
    }

    /// <summary>
    /// Multiplies the coefficients inside the reconstructed corner by the de-quantization and input scaling factors.
    /// </summary>
    /// <param name="block">Block holding the coefficients.</param>
    /// <param name="dequantBlock">Precomputed dequantization block.</param>
    /// <param name="inputScaleBlock">Input scaling for the requested transform sizes.</param>
    /// <param name="idctWidth">Reconstructed sample count per row.</param>
    /// <param name="idctHeight">Reconstructed sample count per column.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ScaleCorner(ref Block8x8F block, ref Block8x8F dequantBlock, ref Block8x8F inputScaleBlock, int idctWidth, int idctHeight)
    {
        int vectorStride = Block8x8F.GetVectorStride(idctWidth);
        int vectorLimit = Block8x8F.GetVectorLimit(idctHeight);
        for (int vectorIndex = 0; vectorIndex < vectorLimit; vectorIndex += vectorStride)
        {
            block.SetVector(vectorIndex, block.GetVector(vectorIndex) * dequantBlock.GetVector(vectorIndex) * inputScaleBlock.GetVector(vectorIndex));
        }
    }

    /// <summary>
    /// Adds the level shift to the samples inside the reconstructed corner.
    /// </summary>
    /// <param name="block">Block holding the samples.</param>
    /// <param name="idctWidth">Reconstructed sample count per row.</param>
    /// <param name="idctHeight">Reconstructed sample count per column.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LevelShiftCorner(ref Block8x8F block, int idctWidth, int idctHeight)
    {
        int vectorStride = Block8x8F.GetVectorStride(idctWidth);
        int vectorLimit = Block8x8F.GetVectorLimit(idctHeight);
        for (int vectorIndex = 0; vectorIndex < vectorLimit; vectorIndex += vectorStride)
        {
            block.SetVector(vectorIndex, block.GetVector(vectorIndex) + LevelShiftVector);
        }
    }

    /// <summary>
    /// Runs a 1-D IDCT of <paramref name="pointCount"/> points down each of the first
    /// <paramref name="columnCount"/> columns of the block.
    /// </summary>
    /// <param name="block">Block to transform (in-place).</param>
    /// <param name="pointCount">Number of points the 1-D transform covers.</param>
    /// <param name="columnCount">Number of columns to transform.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void IdctColumns(ref Block8x8F block, int pointCount, int columnCount)
    {
        bool bothHalves = Block8x8F.GetVectorStride(columnCount) == 1;

        switch (pointCount)
        {
            case DctSize:
            {
                Idct8x4InPlace(ref block.Row0Left);
                if (bothHalves)
                {
                    Idct8x4InPlace(ref block.Row0Right);
                }

                break;
            }
            case 4:
            {
                Idct4x4InPlace(ref block.Row0Left);
                if (bothHalves)
                {
                    Idct4x4InPlace(ref block.Row0Right);
                }

                break;
            }
            case 2:
            {
                Idct2x4InPlace(ref block.Row0Left);
                if (bothHalves)
                {
                    Idct2x4InPlace(ref block.Row0Right);
                }

                break;
            }
            default:
            {
                // A one-point transform is the identity.
                break;
            }
        }
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
        Vector4 evenTmp12 = ((even1 - even3) * C_1_414213562) - evenSum13;

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
        Vector4 oddTmp10 = (diffOdd0Odd3 * C_N1_082392200) + oddIntermediate;
        Vector4 oddTmp12 = (diffOdd2Odd1 * C_N2_613125930) + oddIntermediate;
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
    /// In-place 1-D IDCT over the first 4 coefficients of a panel, producing 4 samples.
    /// </summary>
    /// <param name="vecRef">Reference to the first Vector4 of the panel.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Idct4x4InPlace(ref Vector4 vecRef)
    {
        Vector4 even0 = Unsafe.Add(ref vecRef, 0 * 2);
        Vector4 odd0 = Unsafe.Add(ref vecRef, 1 * 2);
        Vector4 even1 = Unsafe.Add(ref vecRef, 2 * 2);
        Vector4 odd1 = Unsafe.Add(ref vecRef, 3 * 2);

        Vector4 evenSum = even0 + even1;
        Vector4 evenDiff = even0 - even1;
        Vector4 oddSum = (odd0 * C_1_306562965) + (odd1 * C_0_541196100);
        Vector4 oddDiff = (odd0 * C_0_541196100) - (odd1 * C_1_306562965);

        Unsafe.Add(ref vecRef, 0 * 2) = evenSum + oddSum;
        Unsafe.Add(ref vecRef, 1 * 2) = evenDiff + oddDiff;
        Unsafe.Add(ref vecRef, 2 * 2) = evenDiff - oddDiff;
        Unsafe.Add(ref vecRef, 3 * 2) = evenSum - oddSum;
    }

    /// <summary>
    /// In-place 1-D IDCT over the first 2 coefficients of a panel, producing 2 samples.
    /// </summary>
    /// <param name="vecRef">Reference to the first Vector4 of the panel.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Idct2x4InPlace(ref Vector4 vecRef)
    {
        Vector4 even = Unsafe.Add(ref vecRef, 0 * 2);
        Vector4 odd = Unsafe.Add(ref vecRef, 1 * 2);

        Unsafe.Add(ref vecRef, 0 * 2) = even + odd;
        Unsafe.Add(ref vecRef, 1 * 2) = even - odd;
    }

    /// <summary>
    /// Transposes the upper-left square of the given edge length in place.
    /// </summary>
    /// <param name="block">Block to transpose.</param>
    /// <param name="size">Edge length of the square to transpose.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TransposeCorner(ref Block8x8F block, int size)
    {
        if (size == DctSize)
        {
            block.Transpose();
            return;
        }

        for (int row = 1; row < size; row++)
        {
            for (int column = 0; column < row; column++)
            {
                int lower = (row * DctSize) + column;
                int upper = (column * DctSize) + row;
                float swapped = block[lower];
                block[lower] = block[upper];
                block[upper] = swapped;
            }
        }
    }

    /// <summary>
    /// Index of the input scaling block belonging to a pair of transform sizes.
    /// </summary>
    /// <param name="idctWidth">Reconstructed sample count per row.</param>
    /// <param name="idctHeight">Reconstructed sample count per column.</param>
    /// <returns>Index into <see cref="ReducedInputScaleBlocks"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ScaleBlockIndex(int idctWidth, int idctHeight) => (SizeIndex(idctHeight) * 4) + SizeIndex(idctWidth);

    /// <summary>
    /// Base-two logarithm of a transform size.
    /// </summary>
    /// <param name="size">Transform size (1, 2, 4 or 8).</param>
    /// <returns>Index in range [0, 3].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SizeIndex(int size)
    {
        return size switch
        {
            1 => 0,
            2 => 1,
            4 => 2,
            _ => 3
        };
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
        BuildAanScaleFactors(scaleFactors);

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

    /// <summary>
    /// Builds the input scaling blocks for the reduced transforms, one per pair of transform sizes.
    /// </summary>
    /// <returns>Scaling blocks indexed by <see cref="ScaleBlockIndex"/>.</returns>
    private static Block8x8F[] BuildReducedInputScaleBlocks()
    {
        Span<float> aanScaleFactors = stackalloc float[DctSize];
        BuildAanScaleFactors(aanScaleFactors);
        Span<float> rowFactors = stackalloc float[DctSize];
        Span<float> columnFactors = stackalloc float[DctSize];

        var scaleBlocks = new Block8x8F[16];
        for (int heightIndex = 0; heightIndex < 4; heightIndex++)
        {
            BuildPassFactors(1 << heightIndex, aanScaleFactors, rowFactors);
            for (int widthIndex = 0; widthIndex < 4; widthIndex++)
            {
                BuildPassFactors(1 << widthIndex, aanScaleFactors, columnFactors);

                Block8x8F scaleBlock = default;
                int linearIndex = 0;
                for (int rowIndex = 0; rowIndex < DctSize; rowIndex++)
                {
                    for (int columnIndex = 0; columnIndex < DctSize; columnIndex++)
                    {
                        scaleBlock[linearIndex] = rowFactors[rowIndex] * columnFactors[columnIndex];
                        linearIndex++;
                    }
                }

                scaleBlocks[(heightIndex * 4) + widthIndex] = scaleBlock;
            }
        }

        return scaleBlocks;
    }

    /// <summary>
    /// Fills the AAN pre-multipliers of one dimension.
    /// </summary>
    /// <param name="scaleFactors">Destination, one entry per coefficient.</param>
    private static void BuildAanScaleFactors(in Span<float> scaleFactors)
    {
        scaleFactors[0] = 1f;
        for (int k = 1; k < DctSize; k++)
        {
            scaleFactors[k] = MathF.Cos(k * MathF.PI / 16f) * MathF.Sqrt(2f);
        }
    }

    /// <summary>
    /// Fills the input factors one pass contributes for the given transform size. The AAN kernel needs its
    /// pre-multipliers; the four-, two- and one-point kernels take only their share of the normalization.
    /// </summary>
    /// <param name="transformSize">Transform size covering the dimension.</param>
    /// <param name="aanScaleFactors">AAN pre-multipliers.</param>
    /// <param name="passFactors">Destination, one entry per coefficient.</param>
    private static void BuildPassFactors(int transformSize, in ReadOnlySpan<float> aanScaleFactors, in Span<float> passFactors)
    {
        for (int k = 0; k < DctSize; k++)
        {
            passFactors[k] = (transformSize == DctSize) ? aanScaleFactors[k] * PassScale : PassScale;
        }
    }
}
