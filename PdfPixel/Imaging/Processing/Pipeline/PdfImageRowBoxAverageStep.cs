using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Downsamples float samples by box averaging. No axis of the destination grid may be larger than the source grid.
/// </summary>
public sealed class PdfImageRowBoxAverageStep : IPdfImageRowProcessingStep
{
    private readonly int _components;
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private readonly int _destinationWidth;
    private readonly int _destinationHeight;

    private readonly Accumulator[] _destinationRowAccumulators;
    private readonly float[] _buffer;

    // Horizontal: destination→source column ranges, expanded per-component into a flat table.
    // Built from forward midpoint binning so it matches the vertical inverse grid for the same scale.
    private readonly Range[] _sourceSampleRangeForDestinationSample;

    // Vertical: inverse grid. Flags the source rows whose arrival completes a destination row.
    private readonly bool[] _flushAfterSourceRow;

    private int _nextDestinationRowToWrite;
    private int _accumulatedSourceRowCount;

    /// <summary>
    /// Initializes a step averaging a source grid down to a destination grid.
    /// </summary>
    /// <param name="components">Number of channels in each pixel.</param>
    /// <param name="sourceWidth">Number of pixels in each source row.</param>
    /// <param name="destinationWidth">Number of pixels in each destination row.</param>
    /// <param name="sourceHeight">Number of source rows.</param>
    /// <param name="destinationHeight">Number of destination rows.</param>
    public PdfImageRowBoxAverageStep(int components, int sourceWidth, int destinationWidth, int sourceHeight, int destinationHeight)
    {
        if (destinationWidth > sourceWidth || destinationHeight > sourceHeight)
        {
            throw new NotSupportedException("Box averaging only supports downsampling. Upsampling is not allowed.");
        }

        _components = components;
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _destinationWidth = destinationWidth;
        _destinationHeight = destinationHeight;

        int totalDestinationSamples = _destinationWidth * components;
        _destinationRowAccumulators = new Accumulator[totalDestinationSamples];
        _buffer = new float[totalDestinationSamples];

        // Horizontal grid: forward midpoint binning maps each source column to a destination column
        // via (sourceColumn + 0.5) * (destinationWidth / sourceWidth). We walk source columns and
        // emit a destination range whenever the destination column changes, then expand each range
        // per-component into the flat table used by AccumulateSourceRow.
        _sourceSampleRangeForDestinationSample = new Range[_destinationWidth * components];
        float inverseHorizontalScale = (float)_destinationWidth / sourceWidth;
        int currentDestinationColumn = 0;
        int sourceColumnRangeStart = 0;
        for (int sourceColumn = 0; sourceColumn <= sourceWidth; sourceColumn++)
        {
            int destinationColumn;
            if (sourceColumn == sourceWidth)
            {
                destinationColumn = _destinationWidth; // sentinel forces closure of the final range
            }
            else
            {
                destinationColumn = (int)((sourceColumn + 0.5f) * inverseHorizontalScale);
                if (destinationColumn >= _destinationWidth)
                {
                    destinationColumn = _destinationWidth - 1;
                }
            }

            if (destinationColumn != currentDestinationColumn)
            {
                int columnCount = sourceColumn - sourceColumnRangeStart;
                float horizontalReciprocal = (columnCount == 0) ? 0f : 1f / columnCount;

                for (int component = 0; component < components; component++)
                {
                    int destinationIndex = (currentDestinationColumn * components) + component;
                    int flatStart = (sourceColumnRangeStart * components) + component;
                    int flatEnd = (sourceColumn * components) + component;
                    _sourceSampleRangeForDestinationSample[destinationIndex] = new Range(flatStart, flatEnd);
                    _destinationRowAccumulators[destinationIndex].HorizontalReciprocal = horizontalReciprocal;
                }

                sourceColumnRangeStart = sourceColumn;
                currentDestinationColumn = destinationColumn;
            }
        }

        // Vertical inverse grid: same forward midpoint binning. The last source row mapping to a
        // given destination row triggers that row's flush. We walk source rows backwards: the first
        // time we encounter each destination value is its last source row.
        _flushAfterSourceRow = new bool[sourceHeight];
        float inverseVerticalScale = (float)destinationHeight / sourceHeight;
        int previousDestinationRow = -1;
        for (int sourceRow = sourceHeight - 1; sourceRow >= 0; sourceRow--)
        {
            var destinationRow = (int)((sourceRow + 0.5f) * inverseVerticalScale);
            if (destinationRow >= destinationHeight)
            {
                destinationRow = destinationHeight - 1;
            }

            if (destinationRow != previousDestinationRow)
            {
                _flushAfterSourceRow[sourceRow] = true;
                previousDestinationRow = destinationRow;
            }
        }

        _nextDestinationRowToWrite = 0;
    }

    /// <inheritdoc />
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public PdfImageRow Process(ReadOnlySpan<byte> input, int inputRowIndex)
    {
        if (inputRowIndex < 0 || inputRowIndex >= _sourceHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(inputRowIndex), "The input row is outside the source grid.");
        }

        ReadOnlySpan<float> samples = MemoryMarshal.Cast<byte, float>(input);

        if (samples.Length < _sourceWidth * _components)
        {
            throw new ArgumentException("The input row holds fewer samples than the source grid.", nameof(input));
        }

        if (_nextDestinationRowToWrite >= _destinationHeight)
        {
            return PdfImageRow.Empty;
        }

        AccumulateSourceRow(samples);
        _accumulatedSourceRowCount++;

        if (_flushAfterSourceRow[inputRowIndex])
        {
            int destinationRow = _nextDestinationRowToWrite;
            WriteAveragedRow();
            ResetAccumulators();
            _accumulatedSourceRowCount = 0;
            _nextDestinationRowToWrite++;
            return new PdfImageRow(MemoryMarshal.AsBytes(_buffer.AsSpan()), destinationRow, hasNextRow: false);
        }
        else
        {
            return PdfImageRow.Empty;
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AccumulateSourceRow(in ReadOnlySpan<float> samples)
    {
        int totalDestinationSamples = _destinationWidth * _components;
        ref var destinationAccumulatorReference = ref _destinationRowAccumulators[0];
        ref Range sampleRangeReference = ref _sourceSampleRangeForDestinationSample[0];
        ref float sourceReference = ref MemoryMarshal.GetReference(samples);

        for (int destinationIndex = 0; destinationIndex < totalDestinationSamples; destinationIndex++)
        {
            ref readonly Range range = ref Unsafe.Add(ref sampleRangeReference, destinationIndex);

            float sum = 0;

            for (int sourceIndex = range.Start; sourceIndex < range.End; sourceIndex += _components)
            {
                sum += Unsafe.Add(ref sourceReference, sourceIndex);
            }

            destinationAccumulatorReference.Sum += sum;
            destinationAccumulatorReference = ref Unsafe.Add(ref destinationAccumulatorReference, 1);
        }
    }

    // The vertical divisor (source rows accumulated since the last flush) is pure grid geometry
    // and identical for every sample in this row, so it is turned into a single reciprocal here
    // instead of dividing per sample. The horizontal divisor is already baked into each
    // accumulator's HorizontalReciprocal at construction time.
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void WriteAveragedRow()
    {
        float verticalScale = 1f / _accumulatedSourceRowCount;
        ref var accumulatorReference = ref _destinationRowAccumulators[0];
        ref float destinationReference = ref _buffer[0];

        for (int sampleIndex = 0; sampleIndex < _buffer.Length; sampleIndex++)
        {
            Unsafe.Add(ref destinationReference, sampleIndex) = Unsafe.Add(ref accumulatorReference, sampleIndex).GetAverage(verticalScale);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void ResetAccumulators()
    {
        for (int index = 0; index < _destinationRowAccumulators.Length; index++)
        {
            _destinationRowAccumulators[index].Sum = 0;
        }
    }

    private struct Accumulator
    {
        public float Sum;
        public float HorizontalReciprocal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float GetAverage(float verticalScale) => Sum * HorizontalReciprocal * verticalScale;
    }

    private readonly struct Range
    {
        public readonly int Start;
        public readonly int End;

        public Range(int start, int end)
        {
            Start = start;
            End = end;
        }
    }
}
