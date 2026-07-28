using PdfPixel.Jpx.Model;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Inverse 5-3 reversible discrete wavelet transform per ITU-T T.800 Annex F.3.5.
/// Reconstructs component samples from subband coefficients using integer lifting.
/// Includes dequantization (right-shift from MSB-aligned Tier-1 output).
/// </summary>
internal sealed class JpxInverseDwt53 : IJpxInverseDwt
{
    private readonly JpxQuantization _quantization;

    // Reusable interleaved buffer to avoid per-level allocation
    private int[] _interleavedBuffer = Array.Empty<int>();

    /// <summary>
    /// Creates an inverse 5-3 DWT instance with quantization parameters for dequantization.
    /// </summary>
    /// <param name="quantization">Quantization parameters from QCD/QCC marker.</param>
    public JpxInverseDwt53(JpxQuantization quantization) => _quantization = quantization ?? throw new ArgumentNullException(nameof(quantization));

    /// <inheritdoc/>
    public void Transform(JpxSubbandData subbands, in Span<int> destination, int stopAtLevel = 0)
    {
        if (subbands == null)
        {
            throw new ArgumentNullException(nameof(subbands));
        }


        // Compute output dimensions based on how many levels we reconstruct
        int outputWidth = subbands.GetResolutionWidth(stopAtLevel);
        int outputHeight = subbands.GetResolutionHeight(stopAtLevel);

        Span<int> lowpass = subbands.LL;
        int lowpassWidth = subbands.LLWidth;
        int currentWidth = lowpassWidth;
        int currentHeight = subbands.LLHeight;

        // Dequantize LL subband (right-shift from MSB-aligned representation)
        int llShift = JpxDequantizer.ComputeReversibleShift(_quantization, 0);

        // Use destination as working buffer directly — no separate allocation needed
        for (int y = 0; y < currentHeight; y++)
        {
            DequantizeInto(destination.Slice(y * outputWidth, currentWidth), lowpass.Slice(y * lowpassWidth, currentWidth), llShift);
        }

        // Ensure the reusable buffer is large enough
        int maxPixels = outputWidth * outputHeight;
        if (_interleavedBuffer.Length < maxPixels)
        {
            _interleavedBuffer = new int[maxPixels];
        }

        // Reconstruct level by level from coarsest to finest
        for (int level = subbands.Levels - 1; level >= stopAtLevel; level--)
        {
            int nextWidth = subbands.GetResolutionWidth(level);
            int nextHeight = subbands.GetResolutionHeight(level);

            int hlWidth = subbands.GetWidth(level, JpxSubbandType.HL);
            int hlHeight = subbands.GetHeight(level, JpxSubbandType.HL);
            int lhWidth = subbands.GetWidth(level, JpxSubbandType.LH);
            int lhHeight = subbands.GetHeight(level, JpxSubbandType.LH);
            int hhWidth = subbands.GetWidth(level, JpxSubbandType.HH);
            int hhHeight = subbands.GetHeight(level, JpxSubbandType.HH);

            Span<int> hl = subbands.GetSubband(level, JpxSubbandType.HL);
            Span<int> lh = subbands.GetSubband(level, JpxSubbandType.LH);
            Span<int> hh = subbands.GetSubband(level, JpxSubbandType.HH);

            // QCD step size indices: LL=0, then per level (coarsest first) HL, LH, HH
            int qcdBase = 1 + ((subbands.Levels - 1 - level) * 3);
            int hlShift = JpxDequantizer.ComputeReversibleShift(_quantization, qcdBase + 0);
            int lhShift = JpxDequantizer.ComputeReversibleShift(_quantization, qcdBase + 1);
            int hhShift = JpxDequantizer.ComputeReversibleShift(_quantization, qcdBase + 2);

            Span<int> interleaved = _interleavedBuffer;

            // A resolution starting on an odd reference-grid coordinate begins with a high-pass
            // sample, which swaps where each subband lands in the interleaved signal.
            int parityX = subbands.GetResolutionStartX(level) & 1;
            int parityY = subbands.GetResolutionStartY(level) & 1;
            int lowColumn = parityX;
            int highColumn = 1 - parityX;
            int lowRow = parityY;
            int highRow = 1 - parityY;

            // The level is held with each row's two sample classes in contiguous halves rather
            // than interleaved. Nothing before the write-out needs the natural order: the
            // vertical steps work on whole rows, and the horizontal ones read their neighbours
            // from the opposite half, so both walk adjacent samples.
            int lowLength = (nextWidth - parityX + 1) >> 1;

            // Build the level one row at a time. A low-pass row draws its low-pass columns from
            // the already reconstructed LL and its high-pass columns from HL; a high-pass row
            // draws them from LH and HH.
            for (int n = 0; n < nextHeight; n++)
            {
                Span<int> targetRow = interleaved.Slice(n * nextWidth, nextWidth);
                Span<int> lowHalf = targetRow.Slice(0, lowLength);
                Span<int> highHalf = targetRow.Slice(lowLength);

                if ((n & 1) == lowRow)
                {
                    int sourceY = (n - lowRow) >> 1;

                    if (sourceY < currentHeight)
                    {
                        ReadOnlySpan<int> source = destination.Slice(sourceY * outputWidth, currentWidth);
                        source.Slice(0, Math.Min(source.Length, lowHalf.Length)).CopyTo(lowHalf);
                    }

                    if (sourceY < hlHeight)
                    {
                        DequantizeInto(highHalf, hl.Slice(sourceY * hlWidth, hlWidth), hlShift);
                    }
                }
                else
                {
                    int sourceY = (n - highRow) >> 1;

                    if (sourceY < lhHeight)
                    {
                        DequantizeInto(lowHalf, lh.Slice(sourceY * lhWidth, lhWidth), lhShift);
                    }

                    if (sourceY < hhHeight)
                    {
                        DequantizeInto(highHalf, hh.Slice(sourceY * hhWidth, hhWidth), hhShift);
                    }
                }
            }

            // Apply 1D inverse 5-3 filter on rows then columns
            for (int y = 0; y < nextHeight; y++)
            {
                InverseLiftRow(interleaved.Slice(y * nextWidth, nextWidth), lowLength, parityX);
            }

            InverseLiftColumns(interleaved.Slice(0, nextWidth * nextHeight), nextWidth, nextHeight, parityY);

            // Copy the result back, returning each class to its interleaved position. A
            // degenerate tile can reduce to a resolution with no columns at all, which leaves
            // no class positions to write and no row to slice.
            for (int y = 0; y < nextHeight && nextWidth > 0; y++)
            {
                Span<int> sourceRow = interleaved.Slice(y * nextWidth, nextWidth);
                Span<int> targetRow = destination.Slice(y * outputWidth, nextWidth);

                Interleave(sourceRow.Slice(0, lowLength), targetRow.Slice(lowColumn));
                Interleave(sourceRow.Slice(lowLength), targetRow.Slice(highColumn));
            }

            currentWidth = nextWidth;
            currentHeight = nextHeight;
        }
    }

    /// <summary>
    /// Dequantizes subband coefficients into consecutive positions of <paramref name="target"/>.
    /// </summary>
    private static void DequantizeInto(in Span<int> target, in ReadOnlySpan<int> source, int shiftBits)
    {
        int length = Math.Min(source.Length, target.Length);

        ref int coefficient = ref MemoryMarshal.GetReference(source);
        ref int sample = ref MemoryMarshal.GetReference(target);

        for (int x = 0; x < length; x++)
        {
            sample = JpxDequantizer.DequantizeReversible(coefficient, shiftBits);
            coefficient = ref Unsafe.Add(ref coefficient, 1);
            sample = ref Unsafe.Add(ref sample, 1);
        }
    }

    /// <summary>
    /// Returns consecutive samples to every other position of <paramref name="target"/>,
    /// starting at its first.
    /// </summary>
    private static void Interleave(in ReadOnlySpan<int> source, in Span<int> target)
    {
        ref int sourceSample = ref MemoryMarshal.GetReference(source);
        ref int targetSample = ref MemoryMarshal.GetReference(target);

        for (int i = 0; i < source.Length; i++)
        {
            targetSample = sourceSample;
            sourceSample = ref Unsafe.Add(ref sourceSample, 1);
            targetSample = ref Unsafe.Add(ref targetSample, 2);
        }
    }

    /// <summary>
    /// Applies the inverse 5-3 lifting steps down every column of an interleaved level.
    /// Each step is pointwise across the row, so whole rows are lifted against their
    /// neighbouring rows and no column ever has to be gathered into a buffer.
    /// </summary>
    /// <param name="samples">Interleaved level, row-major, <paramref name="height"/> rows of <paramref name="width"/>.</param>
    /// <param name="width">Number of samples per row.</param>
    /// <param name="height">Number of rows.</param>
    /// <param name="parity">
    /// 0 when the first row is a low-pass one, 1 when it is a high-pass one.
    /// </param>
    private static void InverseLiftColumns(in Span<int> samples, int width, int height, int parity)
    {
        if (height == 1)
        {
            // A lone row is passed through when it is low-pass, and halved when it is not.
            if (parity == 1)
            {
                for (int x = 0; x < width; x++)
                {
                    samples[x] >>= 1;
                }
            }

            return;
        }

        // Undo the update step on the low-pass rows, then the predict step on the high-pass
        // ones, which reads the low-pass values the first pass has already restored.
        UpdateRows(samples, width, height, parity);
        PredictRows(samples, width, height, 1 - parity);
    }

    private static void UpdateRows(in Span<int> samples, int width, int height, int firstRow)
    {
        for (int n = firstRow; n < height; n += 2)
        {
            Span<int> current = samples.Slice(n * width, width);
            ReadOnlySpan<int> above = samples.Slice(MirrorRow(n - 1, height) * width, width);
            ReadOnlySpan<int> below = samples.Slice(MirrorRow(n + 1, height) * width, width);

            ref int currentSample = ref MemoryMarshal.GetReference(current);
            ref int aboveSample = ref MemoryMarshal.GetReference(above);
            ref int belowSample = ref MemoryMarshal.GetReference(below);

            for (int x = 0; x < width; x++)
            {
                currentSample -= (aboveSample + belowSample + 2) >> 2;
                currentSample = ref Unsafe.Add(ref currentSample, 1);
                aboveSample = ref Unsafe.Add(ref aboveSample, 1);
                belowSample = ref Unsafe.Add(ref belowSample, 1);
            }
        }
    }

    private static void PredictRows(in Span<int> samples, int width, int height, int firstRow)
    {
        for (int n = firstRow; n < height; n += 2)
        {
            Span<int> current = samples.Slice(n * width, width);
            ReadOnlySpan<int> above = samples.Slice(MirrorRow(n - 1, height) * width, width);
            ReadOnlySpan<int> below = samples.Slice(MirrorRow(n + 1, height) * width, width);

            ref int currentSample = ref MemoryMarshal.GetReference(current);
            ref int aboveSample = ref MemoryMarshal.GetReference(above);
            ref int belowSample = ref MemoryMarshal.GetReference(below);

            for (int x = 0; x < width; x++)
            {
                currentSample += (aboveSample + belowSample) >> 1;
                currentSample = ref Unsafe.Add(ref currentSample, 1);
                aboveSample = ref Unsafe.Add(ref aboveSample, 1);
                belowSample = ref Unsafe.Add(ref belowSample, 1);
            }
        }
    }

    /// <summary>
    /// Maps a row index onto the periodic symmetric extension of the signal per ITU-T T.800 F.3.7.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MirrorRow(int index, int height)
    {
        if (index < 0)
        {
            return -index;
        }

        if (index >= height)
        {
            return (height << 1) - index - 2;
        }

        return index;
    }

    /// <summary>
    /// Applies the inverse 5-3 lifting steps to one line, per ITU-T T.800 F.3.8.2.
    /// </summary>
    /// <remarks>
    /// The line arrives with its two sample classes already in contiguous halves, so every
    /// lifting step reads its two neighbours from adjacent positions of the other half rather
    /// than from alternating positions of the line.
    /// </remarks>
    /// <param name="row">Low-pass samples followed by high-pass samples.</param>
    /// <param name="lowLength">Number of low-pass samples at the front of the row.</param>
    /// <param name="parity">
    /// 0 when the line starts on a low-pass sample, 1 when it starts on a high-pass one.
    /// </param>
    private static void InverseLiftRow(in Span<int> row, int lowLength, int parity)
    {
        if (row.Length == 0)
        {
            return;
        }

        Span<int> low = row.Slice(0, lowLength);
        Span<int> high = row.Slice(lowLength);

        if (row.Length == 1)
        {
            // A lone sample is passed through when it is low-pass, and halved when it is not.
            if (low.Length == 0)
            {
                high[0] >>= 1;
            }

            return;
        }

        // Undo the update step on the low-pass samples, then the predict step on the high-pass
        // ones, which reads the low-pass values the first step has already restored. A class
        // whose samples sit to the right of their neighbours reads the pair ending at its own
        // index, and the one to the left reads the pair starting there, so the parity of the
        // line decides which form each step takes.
        if (parity == 0)
        {
            UpdateFromLeft(low, high);
            PredictFromRight(high, low);
        }
        else
        {
            UpdateFromRight(low, high);
            PredictFromLeft(high, low);
        }
    }

    /// <summary>
    /// Undoes the update step for a class whose samples follow their neighbours.
    /// </summary>
    private static void UpdateFromLeft(in Span<int> target, in ReadOnlySpan<int> source)
    {
        int last = source.Length - 1;
        int interior = Math.Min(target.Length, source.Length);

        // The neighbour before the first sample mirrors back onto the first source sample.
        target[0] -= (source[0] + source[0] + 2) >> 2;

        ref int targetSample = ref Unsafe.Add(ref MemoryMarshal.GetReference(target), 1);
        ref int sourceSample = ref MemoryMarshal.GetReference(source);

        for (int i = 1; i < interior; i++)
        {
            targetSample -= (sourceSample + Unsafe.Add(ref sourceSample, 1) + 2) >> 2;
            targetSample = ref Unsafe.Add(ref targetSample, 1);
            sourceSample = ref Unsafe.Add(ref sourceSample, 1);
        }

        // A longer target than source leaves samples whose later neighbour mirrors back.
        for (int i = interior; i < target.Length; i++)
        {
            target[i] -= (source[last] + source[last] + 2) >> 2;
        }
    }

    /// <summary>
    /// Undoes the update step for a class whose samples precede their neighbours.
    /// </summary>
    private static void UpdateFromRight(in Span<int> target, in ReadOnlySpan<int> source)
    {
        int last = source.Length - 1;
        int interior = Math.Min(target.Length, last);

        ref int targetSample = ref MemoryMarshal.GetReference(target);
        ref int sourceSample = ref MemoryMarshal.GetReference(source);

        for (int i = 0; i < interior; i++)
        {
            targetSample -= (sourceSample + Unsafe.Add(ref sourceSample, 1) + 2) >> 2;
            targetSample = ref Unsafe.Add(ref targetSample, 1);
            sourceSample = ref Unsafe.Add(ref sourceSample, 1);
        }

        // The neighbour after the last sample mirrors back onto the last source sample.
        for (int i = interior; i < target.Length; i++)
        {
            target[i] -= (source[last] + source[last] + 2) >> 2;
        }
    }

    /// <summary>
    /// Undoes the predict step for a class whose samples follow their neighbours.
    /// </summary>
    private static void PredictFromLeft(in Span<int> target, in ReadOnlySpan<int> source)
    {
        int last = source.Length - 1;
        int interior = Math.Min(target.Length, source.Length);

        // The neighbour before the first sample mirrors back onto the first source sample.
        target[0] += (source[0] + source[0]) >> 1;

        ref int targetSample = ref Unsafe.Add(ref MemoryMarshal.GetReference(target), 1);
        ref int sourceSample = ref MemoryMarshal.GetReference(source);

        for (int i = 1; i < interior; i++)
        {
            targetSample += (sourceSample + Unsafe.Add(ref sourceSample, 1)) >> 1;
            targetSample = ref Unsafe.Add(ref targetSample, 1);
            sourceSample = ref Unsafe.Add(ref sourceSample, 1);
        }

        // A longer target than source leaves samples whose later neighbour mirrors back.
        for (int i = interior; i < target.Length; i++)
        {
            target[i] += (source[last] + source[last]) >> 1;
        }
    }

    /// <summary>
    /// Undoes the predict step for a class whose samples precede their neighbours.
    /// </summary>
    private static void PredictFromRight(in Span<int> target, in ReadOnlySpan<int> source)
    {
        int last = source.Length - 1;
        int interior = Math.Min(target.Length, last);

        ref int targetSample = ref MemoryMarshal.GetReference(target);
        ref int sourceSample = ref MemoryMarshal.GetReference(source);

        for (int i = 0; i < interior; i++)
        {
            targetSample += (sourceSample + Unsafe.Add(ref sourceSample, 1)) >> 1;
            targetSample = ref Unsafe.Add(ref targetSample, 1);
            sourceSample = ref Unsafe.Add(ref sourceSample, 1);
        }

        // The neighbour after the last sample mirrors back onto the last source sample.
        for (int i = interior; i < target.Length; i++)
        {
            target[i] += (source[last] + source[last]) >> 1;
        }
    }
}
