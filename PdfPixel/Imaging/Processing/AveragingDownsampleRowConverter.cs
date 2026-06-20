using PdfPixel.Parsing;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Row converter that performs simple box averaging for downsampling.
/// Accumulates source pixel values in integer buckets and averages them for each destination pixel.
/// Always outputs 8-bit samples. Optimized for performance with minimal allocations.
/// </summary>
internal sealed class AveragingDownsampleRowConverter : IRowConverter
{
    private readonly int _components;
    private readonly int _sourceBitsPerComponent;
    private readonly int _sourceWidth;
    private readonly int _destinationWidth;
    private readonly int _destinationHeight;

    private readonly Accumulator[] _destinationRowAccumulators;
    private readonly uint[] _sourceSamples;

    // Horizontal: destination→source column ranges, expanded per-component into a flat table.
    // Built from forward midpoint binning so it matches the vertical inverse grid for the same scale.
    private readonly Range[] _sourceSampleRangeForDestinationSample;

    // Vertical: inverse grid. Set of source-row indices whose arrival completes a destination row.
    private readonly HashSet<int> _flushAfterSourceRow;

    private int _nextDestinationRowToWrite;

    private readonly uint _sourceMaximumValue;
    private readonly float _sourceToDestinationScale;

    public AveragingDownsampleRowConverter(int components, int sourceBitsPerComponent, int sourceWidth, int destinationWidth, int sourceHeight, int destinationHeight)
    {
        if (destinationWidth > sourceWidth || destinationHeight > sourceHeight)
        {
            throw new NotSupportedException("Averaging downsample row converter only supports downsampling. Upsampling is not allowed.");
        }

        if (sourceBitsPerComponent > 16)
        {
            throw new ArgumentException("Source bits per component must be 16 or less.", nameof(sourceBitsPerComponent));
        }

        _components = components;
        _sourceBitsPerComponent = sourceBitsPerComponent;
        _sourceWidth = sourceWidth;
        _destinationWidth = destinationWidth;
        _destinationHeight = destinationHeight;

        _sourceMaximumValue = (1u << sourceBitsPerComponent) - 1u;
        _sourceToDestinationScale = (_sourceMaximumValue == 0) ? 0f : 255f / _sourceMaximumValue;

        int totalDestinationSamples = _destinationWidth * components;
        _destinationRowAccumulators = new Accumulator[totalDestinationSamples];
        _sourceSamples = new uint[_sourceWidth * components];

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
                for (int component = 0; component < components; component++)
                {
                    int destinationIndex = (currentDestinationColumn * components) + component;
                    int flatStart = (sourceColumnRangeStart * components) + component;
                    int flatEnd = (sourceColumn * components) + component;
                    _sourceSampleRangeForDestinationSample[destinationIndex] = new Range(flatStart, flatEnd);
                }

                sourceColumnRangeStart = sourceColumn;
                currentDestinationColumn = destinationColumn;
            }
        }

        // Vertical inverse grid: same forward midpoint binning. The last source row mapping to a
        // given destination row triggers that row's flush. We walk source rows backwards: the first
        // time we encounter each destination value is its last source row.
        _flushAfterSourceRow = new HashSet<int>();
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
                _flushAfterSourceRow.Add(sourceRow);
                previousDestinationRow = destinationRow;
            }
        }

        _nextDestinationRowToWrite = 0;
    }

    public bool TryConvertRow(int rowIndex, ReadOnlySpan<byte> sourceRow, Span<byte> destRow)
    {
        if (_nextDestinationRowToWrite >= _destinationHeight)
        {
            return false;
        }

        ReadSourceRowSamples(sourceRow);
        AccumulateSourceRow();

        if (_flushAfterSourceRow.Contains(rowIndex))
        {
            WriteAveragedRow(destRow);
            ResetAccumulators();
            _nextDestinationRowToWrite++;
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadSourceRowSamples(in ReadOnlySpan<byte> sourceRow)
    {
        int totalSourceSamples = _sourceWidth * _components;
        UintBitReaderFixedLength reader = new(sourceRow, _sourceBitsPerComponent);
        for (int sampleIndex = 0; sampleIndex < totalSourceSamples; sampleIndex++)
        {
            _sourceSamples[sampleIndex] = reader.Read();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AccumulateSourceRow()
    {
        int totalDestinationSamples = _destinationWidth * _components;
        ref var destinationAccumulatorReference = ref _destinationRowAccumulators[0];
        ref Range sampleRangeReference = ref _sourceSampleRangeForDestinationSample[0];
        ref uint sourceSamplesReference = ref _sourceSamples[0];

        for (int destinationIndex = 0; destinationIndex < totalDestinationSamples; destinationIndex++)
        {
            ref readonly Range range = ref Unsafe.Add(ref sampleRangeReference, destinationIndex);

            long sum = 0;
            int count = 0;

            for (int sourceIndex = range.Start; sourceIndex < range.End; sourceIndex += _components)
            {
                sum += Unsafe.Add(ref sourceSamplesReference, sourceIndex);
                count++;
            }

            destinationAccumulatorReference.Add(sum, count);
            destinationAccumulatorReference = ref Unsafe.Add(ref destinationAccumulatorReference, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteAveragedRow(in Span<byte> destRow)
    {
        int totalSamples = _destinationWidth * _components;
        UintBitWriter writer = new(destRow);
        for (int sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
        {
            byte value = _destinationRowAccumulators[sampleIndex].GetAverage(_sourceToDestinationScale);
            writer.Write8Bits(value);
        }

        writer.Flush();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetAccumulators() => Array.Clear(_destinationRowAccumulators, 0, _destinationRowAccumulators.Length);

    private struct Accumulator
    {
        public long Sum;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(long value, int count)
        {
            Sum += value;
            Count += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte GetAverage(float scale)
        {
            if (Count == 0)
            {
                return 0;
            }

            float average = (Sum / (float)Count) * scale;
            if (average < 0)
            {
                return 0;
            }

            if (average > 255)
            {
                return 255;
            }

            return (byte)average;
        }
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
