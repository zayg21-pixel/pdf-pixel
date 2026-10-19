using PdfPixel.Jpx.Model;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Inverse 9-7 irreversible discrete wavelet transform per ITU-T T.800 Annex F.3.6
/// with integrated inverse quantization. The 9-7 kernel is defined on real numbers,
/// so all arithmetic uses single-precision floats and the samples are rounded to
/// integers once, when the reconstructed component is written out.
/// </summary>
internal sealed class JpxInverseDwt97 : IJpxInverseDwt
{
    // CDF 9/7 lifting coefficients, as the 13-bit approximations the fixed-point
    // implementation used. The exact ITU-T T.800 Table F.4 values shift the decoded
    // samples by ~0.15 on average, which has not been shown to be an improvement.
    private const float Alpha = -12994f / 8192f;
    private const float Beta = -434f / 8192f;
    private const float Gamma = 7233f / 8192f;
    private const float Delta = 3633f / 8192f;
    private const float K = 10078f / 8192f;
    private const float InvK = 6659f / 8192f;

    private readonly JpxQuantization _quantization;
    private readonly int _bitDepth;

    /// <summary>
    /// Creates an inverse 9-7 DWT instance with the quantization parameters needed
    /// to dequantize coefficients during interleaving.
    /// </summary>
    /// <param name="quantization">Quantization parameters from QCD/QCC marker.</param>
    /// <param name="bitDepth">Component bit depth for computing nominal dynamic range.</param>
    public JpxInverseDwt97(JpxQuantization quantization, int bitDepth)
    {
        _quantization = quantization;
        _bitDepth = bitDepth;
    }

    /// <inheritdoc/>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void Transform(JpxSubbandData subbands, JpxDwtScratch scratch, int stopAtLevel = 0)
    {
        if (subbands == null)
        {
            throw new ArgumentNullException(nameof(subbands));
        }

        if (scratch == null)
        {
            throw new ArgumentNullException(nameof(scratch));
        }

        int levels = subbands.Levels;

        // The coefficients are replaced by their dequantized values in place, as floats of the same width.
        Span<int> coefficients = subbands.Samples;
        Span<float> samples = MemoryMarshal.Cast<int, float>(coefficients);
        int stride = subbands.Stride;
        int outputHeight = subbands.GetResolutionHeight(stopAtLevel);

        float llScale = JpxDequantizer.ComputeIrreversibleScale(_quantization, 0, 0, _bitDepth);
        DequantizeSubband(samples, coefficients, 0, stride, subbands.LLWidth, subbands.LLHeight, llScale);

        // One buffer serves both passes: a strip of columns vertically, a row at a time horizontally.
        float[] lineBuffer = scratch.GetInterleavedSamples(Math.Max(stride, JpxDwtScratch.ColumnStripWidth * outputHeight));

        // Reconstruct level by level from coarsest to finest
        for (int level = levels - 1; level >= stopAtLevel; level--)
        {
            int nextWidth = subbands.GetResolutionWidth(level);
            int nextHeight = subbands.GetResolutionHeight(level);

            // QCD step size indices: LL=0, then coarsest to finest detail
            // level (levels-1) → indices 1,2,3; level (levels-2) → 4,5,6; etc.
            int qcdBase = 1 + ((levels - 1 - level) * 3);
            float hlScale = JpxDequantizer.ComputeIrreversibleScale(_quantization, qcdBase + 0, 1, _bitDepth);
            float lhScale = JpxDequantizer.ComputeIrreversibleScale(_quantization, qcdBase + 1, 1, _bitDepth);
            float hhScale = JpxDequantizer.ComputeIrreversibleScale(_quantization, qcdBase + 2, 2, _bitDepth);

            DequantizeSubband(
                samples,
                coefficients,
                subbands.GetOffset(level, JpxSubbandType.HL),
                stride,
                subbands.GetWidth(level, JpxSubbandType.HL),
                subbands.GetHeight(level, JpxSubbandType.HL),
                hlScale);
            DequantizeSubband(
                samples,
                coefficients,
                subbands.GetOffset(level, JpxSubbandType.LH),
                stride,
                subbands.GetWidth(level, JpxSubbandType.LH),
                subbands.GetHeight(level, JpxSubbandType.LH),
                lhScale);
            DequantizeSubband(
                samples,
                coefficients,
                subbands.GetOffset(level, JpxSubbandType.HH),
                stride,
                subbands.GetWidth(level, JpxSubbandType.HH),
                subbands.GetHeight(level, JpxSubbandType.HH),
                hhScale);

            // A resolution starting on an odd reference-grid coordinate begins with a high-pass
            // sample, which swaps where each subband lands in the interleaved signal.
            int parityX = subbands.GetResolutionStartX(level) & 1;
            int parityY = subbands.GetResolutionStartY(level) & 1;
            int lowColumn = parityX;
            int highColumn = 1 - parityX;

            // The level is held with each row's two sample classes in contiguous halves and its
            // low-pass rows above its high-pass ones. The horizontal steps read their neighbours
            // from the opposite half, so they run a vector at a time over adjacent samples.
            int lowLength = (nextWidth - parityX + 1) >> 1;
            int lowRows = (nextHeight - parityY + 1) >> 1;

            // Apply the 1D inverse 9-7 filter on columns, a strip at a time, returning each row
            // class to its interleaved position.
            for (int x = 0; x < nextWidth; x += JpxDwtScratch.ColumnStripWidth)
            {
                int stripWidth = Math.Min(JpxDwtScratch.ColumnStripWidth, nextWidth - x);
                Span<float> strip = lineBuffer.AsSpan(0, stripWidth * nextHeight);

                GatherStrip(samples, stride, x, strip, stripWidth, nextHeight, lowRows, parityY);
                InverseLiftColumns(strip, stripWidth, nextHeight, parityY);
                ScatterStrip(strip, samples, stride, x, stripWidth, nextHeight);
            }

            // Then on rows, returning each column class to its interleaved position. A
            // degenerate tile can reduce to a resolution with no columns at all, which leaves
            // no class positions to write and no row to slice.
            for (int y = 0; y < nextHeight && nextWidth > 0; y++)
            {
                Span<float> row = samples.Slice(y * stride, nextWidth);
                Span<float> line = lineBuffer.AsSpan(0, nextWidth);

                InverseLiftRow(row, lowLength, parityX);
                row.CopyTo(line);

                Interleave(line.Slice(0, lowLength), row.Slice(lowColumn));
                Interleave(line.Slice(lowLength), row.Slice(highColumn));
            }
        }

        // Round the reconstructed samples to the nearest integer. Offsetting by a half that
        // carries the sample's own sign turns the conversion's truncation toward zero into
        // round-to-nearest, which needs no floor and so vectorizes on every target framework.
        int totalPixels = stride * outputHeight;
        int i = 0;

        if (Vector.IsHardwareAccelerated && totalPixels >= Vector<float>.Count)
        {
            Vector<int> signMask = new(unchecked((int)0x80000000));
            Vector<int> halfBits = Vector.AsVectorInt32(new Vector<float>(0.5f));
            int lastBlock = totalPixels - Vector<float>.Count;

            for (; i <= lastBlock; i += Vector<float>.Count)
            {
                Vector<float> value = ReadVectorAt(samples, i);
                Vector<int> sign = Vector.BitwiseAnd(Vector.AsVectorInt32(value), signMask);
                Vector<float> offset = Vector.AsVectorSingle(Vector.BitwiseOr(halfBits, sign));

                VectorAt(coefficients, i) = Vector.ConvertToInt32(value + offset);
            }
        }

        for (; i < totalPixels; i++)
        {
            float sample = samples[i];
            coefficients[i] = (int)(sample + ((sample < 0) ? -0.5f : 0.5f));
        }
    }

    /// <summary>
    /// Dequantizes one subband in place, replacing each coefficient with its float value.
    /// </summary>
    private static void DequantizeSubband(in Span<float> samples, in ReadOnlySpan<int> coefficients, int offset, int stride, int width, int height, float scale)
    {
        for (int y = 0; y < height; y++)
        {
            int rowStart = offset + (y * stride);
            DequantizeInto(samples.Slice(rowStart, width), coefficients.Slice(rowStart, width), scale);
        }
    }

    /// <summary>
    /// Copies <paramref name="width"/> columns starting at <paramref name="x"/> into
    /// <paramref name="strip"/>, low-pass and high-pass rows interleaved.
    /// </summary>
    private static void GatherStrip(in ReadOnlySpan<float> samples, int stride, int x, in Span<float> strip, int width, int height, int lowRows, int parity)
    {
        for (int n = 0; n < height; n++)
        {
            int sourceRow = ((n & 1) == parity)
                ? (n - parity) >> 1
                : lowRows + ((n - 1 + parity) >> 1);

            samples.Slice((sourceRow * stride) + x, width).CopyTo(strip.Slice(n * width, width));
        }
    }

    /// <summary>
    /// Copies <paramref name="strip"/> back to <paramref name="width"/> columns starting at
    /// <paramref name="x"/>, row for row.
    /// </summary>
    private static void ScatterStrip(in ReadOnlySpan<float> strip, in Span<float> samples, int stride, int x, int width, int height)
    {
        for (int n = 0; n < height; n++)
        {
            strip.Slice(n * width, width).CopyTo(samples.Slice((n * stride) + x, width));
        }
    }

    /// <summary>
    /// Dequantizes subband coefficients into consecutive positions of <paramref name="target"/>.
    /// </summary>
    /// <remarks>
    /// Tier-1 leaves the sign in bit 31 and the magnitude below it, which is where IEEE-754
    /// keeps its own sign, so the scaled magnitude only has to have that bit put back rather
    /// than be negated under a branch.
    /// </remarks>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void DequantizeInto(in Span<float> target, in ReadOnlySpan<int> source, float scale)
    {
        int length = Math.Min(source.Length, target.Length);
        int x = 0;

        if (Vector.IsHardwareAccelerated && length >= Vector<float>.Count)
        {
            Vector<float> factor = new(scale);
            Vector<int> signMask = new(unchecked((int)0x80000000));
            Vector<int> magnitudeMask = new(0x7FFFFFFF);
            int lastBlock = length - Vector<float>.Count;

            for (; x <= lastBlock; x += Vector<float>.Count)
            {
                Vector<int> coefficients = ReadVectorAt(source, x);
                Vector<int> magnitude = Vector.BitwiseAnd(coefficients, magnitudeMask);
                Vector<float> scaled = Vector.ConvertToSingle(magnitude) * factor;
                Vector<int> signBit = Vector.BitwiseAnd(coefficients, signMask);

                VectorAt(target, x) = Vector.AsVectorSingle(Vector.BitwiseOr(Vector.AsVectorInt32(scaled), signBit));
            }
        }

        for (; x < length; x++)
        {
            target[x] = JpxDequantizer.DequantizeIrreversible(source[x], scale);
        }
    }

    /// <summary>
    /// Applies the inverse 9-7 lifting steps down every column of an interleaved strip.
    /// Each step is pointwise across the row, so whole rows are lifted against their
    /// neighbouring rows.
    /// </summary>
    /// <param name="samples">Interleaved strip, row-major, <paramref name="height"/> rows of <paramref name="width"/>.</param>
    /// <param name="width">Number of samples per row.</param>
    /// <param name="height">Number of rows.</param>
    /// <param name="parity">
    /// 0 when the first row is a low-pass one, 1 when it is a high-pass one.
    /// </param>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void InverseLiftColumns(in Span<float> samples, int width, int height, int parity)
    {
        if (height == 1)
        {
            return;
        }

        // Undo the scaling the forward transform applied to each sample class.
        for (int n = parity; n < height; n += 2)
        {
            ScaleRow(samples.Slice(n * width, width), K);
        }

        for (int n = 1 - parity; n < height; n += 2)
        {
            ScaleRow(samples.Slice(n * width, width), InvK);
        }

        // Undo the four lifting steps in reverse of the order the forward transform applied them.
        LiftRows(samples, width, height, parity, Delta);
        LiftRows(samples, width, height, 1 - parity, Gamma);
        LiftRows(samples, width, height, parity, Beta);
        LiftRows(samples, width, height, 1 - parity, Alpha);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ScaleRow(in Span<float> row, float coefficient)
    {
        int width = row.Length;
        int x = 0;

        if (Vector.IsHardwareAccelerated && width >= Vector<float>.Count)
        {
            Vector<float> factor = new(coefficient);
            int lastBlock = width - Vector<float>.Count;

            for (; x <= lastBlock; x += Vector<float>.Count)
            {
                VectorAt(row, x) *= factor;
            }
        }

        for (; x < width; x++)
        {
            row[x] *= coefficient;
        }
    }

    /// <summary>
    /// Subtracts one lifting step's contribution from every row of one class.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void LiftRows(in Span<float> samples, int width, int height, int firstRow, float coefficient)
    {
        for (int n = firstRow; n < height; n += 2)
        {
            Span<float> current = samples.Slice(n * width, width);
            Span<float> above = samples.Slice(MirrorRow(n - 1, height) * width, width);
            Span<float> below = samples.Slice(MirrorRow(n + 1, height) * width, width);

            int x = 0;

            if (Vector.IsHardwareAccelerated && width >= Vector<float>.Count)
            {
                Vector<float> factor = new(coefficient);
                int lastBlock = width - Vector<float>.Count;

                for (; x <= lastBlock; x += Vector<float>.Count)
                {
                    VectorAt(current, x) -= factor * (VectorAt(above, x) + VectorAt(below, x));
                }
            }

            for (; x < width; x++)
            {
                current[x] -= coefficient * (above[x] + below[x]);
            }
        }
    }

    /// <summary>
    /// Views the samples at <paramref name="index"/> as one vector, for reading or assignment.
    /// <see cref="Vector{T}"/> gained span-based loads only after netstandard2.0, so the
    /// reference is formed by hand to keep one implementation for every target framework.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Vector<float> VectorAt(in Span<float> samples, int index)
    {
        ref float start = ref Unsafe.Add(ref MemoryMarshal.GetReference(samples), index);

        return ref Unsafe.As<float, Vector<float>>(ref start);
    }

    /// <summary>
    /// Reads one vector of samples. See <see cref="VectorAt(in Span{float}, int)"/> for why
    /// this is done by hand.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<float> ReadVectorAt(in ReadOnlySpan<float> samples, int index)
    {
        ref float start = ref Unsafe.Add(ref MemoryMarshal.GetReference(samples), index);

        return Unsafe.As<float, Vector<float>>(ref start);
    }

    /// <summary>
    /// Views the samples at <paramref name="index"/> as one vector, for assignment.
    /// See <see cref="VectorAt(in Span{float}, int)"/> for why this is done by hand.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Vector<int> VectorAt(in Span<int> samples, int index)
    {
        ref int start = ref Unsafe.Add(ref MemoryMarshal.GetReference(samples), index);

        return ref Unsafe.As<int, Vector<int>>(ref start);
    }

    /// <summary>
    /// Reads one vector of coefficients. See <see cref="VectorAt(in Span{float}, int)"/> for
    /// why this is done by hand.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<int> ReadVectorAt(in ReadOnlySpan<int> coefficients, int index)
    {
        ref int start = ref Unsafe.Add(ref MemoryMarshal.GetReference(coefficients), index);

        return Unsafe.As<int, Vector<int>>(ref start);
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
    /// Applies the inverse 9-7 lifting steps to one interleaved line,
    /// per ITU-T T.800 F.3.8.2.
    /// </summary>
    /// <remarks>
    /// The line arrives with its two sample classes already in contiguous halves, so every
    /// lifting step reads its two neighbours from adjacent positions of the other half rather
    /// than from alternating positions, which is what lets the steps run a vector at a time.
    /// </remarks>
    /// <param name="row">Low-pass samples followed by high-pass samples.</param>
    /// <param name="lowLength">Number of low-pass samples at the front of the row.</param>
    /// <param name="parity">
    /// 0 when the line starts on a low-pass sample, 1 when it starts on a high-pass one.
    /// </param>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void InverseLiftRow(in Span<float> row, int lowLength, int parity)
    {
        // A line of one sample has nothing to lift, and one of none has no class to lift.
        if (row.Length <= 1)
        {
            return;
        }

        Span<float> low = row.Slice(0, lowLength);
        Span<float> high = row.Slice(lowLength);

        // Undo the scaling the forward transform applied to each sample class.
        ScaleRow(low, K);
        ScaleRow(high, InvK);

        // Undo the four lifting steps in reverse of the order the forward transform applied
        // them. A class whose samples sit to the right of their neighbours in the line reads
        // the pair ending at its own index, and the one to the left reads the pair starting
        // there, so the parity of the line decides which form each step takes.
        if (parity == 0)
        {
            LiftFromLeft(low, high, Delta);
            LiftFromRight(high, low, Gamma);
            LiftFromLeft(low, high, Beta);
            LiftFromRight(high, low, Alpha);
        }
        else
        {
            LiftFromRight(low, high, Delta);
            LiftFromLeft(high, low, Gamma);
            LiftFromRight(low, high, Beta);
            LiftFromLeft(high, low, Alpha);
        }
    }

    /// <summary>
    /// Returns consecutive samples to every other position of <paramref name="target"/>,
    /// starting at its first. Walked by reference so neither span is bounds-checked per sample.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void Interleave(in ReadOnlySpan<float> source, in Span<float> target)
    {
        ref float sourceSample = ref MemoryMarshal.GetReference(source);
        ref float targetSample = ref MemoryMarshal.GetReference(target);

        for (int i = 0; i < source.Length; i++)
        {
            targetSample = sourceSample;
            sourceSample = ref Unsafe.Add(ref sourceSample, 1);
            targetSample = ref Unsafe.Add(ref targetSample, 2);
        }
    }

    /// <summary>
    /// Subtracts one lifting step from a class whose samples follow their neighbours, so each
    /// target reads the source pair ending at its own index.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void LiftFromLeft(in Span<float> target, in ReadOnlySpan<float> source, float coefficient)
    {
        int count = target.Length;

        if (count == 0)
        {
            return;
        }

        // The neighbour before the first sample mirrors back onto the first source sample.
        target[0] -= coefficient * (source[0] + source[0]);

        int last = Math.Min(count, source.Length);
        int i = 1;

        if (Vector.IsHardwareAccelerated && last - i >= Vector<float>.Count)
        {
            Vector<float> factor = new(coefficient);
            int lastBlock = last - Vector<float>.Count;

            for (; i <= lastBlock; i += Vector<float>.Count)
            {
                VectorAt(target, i) -= factor * (ReadVectorAt(source, i - 1) + ReadVectorAt(source, i));
            }
        }

        for (; i < last; i++)
        {
            target[i] -= coefficient * (source[i - 1] + source[i]);
        }

        // A longer target than source leaves one sample whose later neighbour mirrors back.
        for (; i < count; i++)
        {
            target[i] -= coefficient * (source[source.Length - 1] + source[source.Length - 1]);
        }
    }

    /// <summary>
    /// Subtracts one lifting step from a class whose samples precede their neighbours, so each
    /// target reads the source pair starting at its own index.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void LiftFromRight(in Span<float> target, in ReadOnlySpan<float> source, float coefficient)
    {
        int count = target.Length;

        if (count == 0)
        {
            return;
        }

        int last = Math.Min(count, source.Length - 1);
        int i = 0;

        if (Vector.IsHardwareAccelerated && last >= Vector<float>.Count)
        {
            Vector<float> factor = new(coefficient);
            int lastBlock = last - Vector<float>.Count;

            for (; i <= lastBlock; i += Vector<float>.Count)
            {
                VectorAt(target, i) -= factor * (ReadVectorAt(source, i) + ReadVectorAt(source, i + 1));
            }
        }

        for (; i < last; i++)
        {
            target[i] -= coefficient * (source[i] + source[i + 1]);
        }

        // The neighbour after the last sample mirrors back onto the last source sample.
        for (; i < count; i++)
        {
            target[i] -= coefficient * (source[source.Length - 1] + source[source.Length - 1]);
        }
    }
}
