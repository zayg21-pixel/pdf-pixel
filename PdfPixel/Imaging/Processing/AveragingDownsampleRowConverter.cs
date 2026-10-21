using PdfPixel.Parsing;
using System;
using System.Buffers.Binary;
#if !NETSTANDARD2_0
using System.Numerics;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    // Vertical: inverse grid. Flags the source rows whose arrival completes a destination row.
    private readonly bool[] _flushAfterSourceRow;

    private int _nextDestinationRowToWrite;
    private int _accumulatedSourceRowCount;
    private int _accumulatedRowIndex = -1;

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

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public bool TryConvertRow(int rowIndex, ReadOnlySpan<byte> sourceRow, int sourceStartBit, Span<byte> destRow)
    {
        if (_nextDestinationRowToWrite >= _destinationHeight || _accumulatedRowIndex == rowIndex)
        {
            return false;
        }

        _accumulatedRowIndex = rowIndex;

        if (_sourceBitsPerComponent == 8 && (sourceStartBit & 7) == 0)
        {
            AccumulateSourceBytes(sourceRow.Slice(sourceStartBit >> 3));
        }
        else if (_sourceBitsPerComponent == 1 && _components == 1)
        {
            AccumulateSourceBits(sourceRow, sourceStartBit);
        }
        else
        {
            ReadSourceRowSamples(sourceRow, sourceStartBit);
            AccumulateSourceRow();
        }

        _accumulatedSourceRowCount++;

        if (_flushAfterSourceRow[rowIndex])
        {
            WriteAveragedRow(destRow);
            ResetAccumulators();
            _accumulatedSourceRowCount = 0;
            _nextDestinationRowToWrite++;
            return true;
        }

        return false;
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void ReadSourceRowSamples(in ReadOnlySpan<byte> sourceRow, int sourceStartBit)
    {
        int totalSourceSamples = _sourceWidth * _components;
        UintBitReaderFixedLength reader = new(sourceRow, _sourceBitsPerComponent, sourceStartBit);
        for (int sampleIndex = 0; sampleIndex < totalSourceSamples; sampleIndex++)
        {
            _sourceSamples[sampleIndex] = reader.Read();
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
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

            for (int sourceIndex = range.Start; sourceIndex < range.End; sourceIndex += _components)
            {
                sum += Unsafe.Add(ref sourceSamplesReference, sourceIndex);
            }

            destinationAccumulatorReference.Sum += sum;
            destinationAccumulatorReference = ref Unsafe.Add(ref destinationAccumulatorReference, 1);
        }
    }

    // 8-bit samples are summed straight from the source row bytes.
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AccumulateSourceBytes(in ReadOnlySpan<byte> sourceRow)
    {
        int totalDestinationSamples = _destinationWidth * _components;
        ref var destinationAccumulatorReference = ref _destinationRowAccumulators[0];
        ref Range sampleRangeReference = ref _sourceSampleRangeForDestinationSample[0];
        ref byte sourceReference = ref MemoryMarshal.GetReference(sourceRow);

        for (int destinationIndex = 0; destinationIndex < totalDestinationSamples; destinationIndex++)
        {
            ref readonly Range range = ref Unsafe.Add(ref sampleRangeReference, destinationIndex);

            int sum = 0;

            for (int sourceIndex = range.Start; sourceIndex < range.End; sourceIndex += _components)
            {
                sum += Unsafe.Add(ref sourceReference, sourceIndex);
            }

            destinationAccumulatorReference.Sum += sum;
            destinationAccumulatorReference = ref Unsafe.Add(ref destinationAccumulatorReference, 1);
        }
    }

    // Single-component 1-bit samples are summed by counting the set bits of each range.
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AccumulateSourceBits(in ReadOnlySpan<byte> sourceRow, int sourceStartBit)
    {
        int totalDestinationSamples = _destinationWidth;
        ref var destinationAccumulatorReference = ref _destinationRowAccumulators[0];
        ref Range sampleRangeReference = ref _sourceSampleRangeForDestinationSample[0];

        for (int destinationIndex = 0; destinationIndex < totalDestinationSamples; destinationIndex++)
        {
            ref readonly Range range = ref Unsafe.Add(ref sampleRangeReference, destinationIndex);

            destinationAccumulatorReference.Sum += CountSetBits(sourceRow, sourceStartBit + range.Start, sourceStartBit + range.End);
            destinationAccumulatorReference = ref Unsafe.Add(ref destinationAccumulatorReference, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountSetBits(in ReadOnlySpan<byte> row, int startBit, int endBit)
    {
        int count = 0;
        int bit = startBit;

        while (bit < endBit)
        {
            int byteIndex = bit >> 3;
            int offset = bit & 7;
            ulong window;

            if (byteIndex + sizeof(ulong) <= row.Length)
            {
                window = BinaryPrimitives.ReadUInt64BigEndian(row.Slice(byteIndex));
            }
            else
            {
                window = 0;
                for (int index = byteIndex; index < row.Length; index++)
                {
                    window |= (ulong)row[index] << (56 - ((index - byteIndex) * 8));
                }
            }

            int take = Math.Min(endBit - bit, 64 - offset);
            ulong bits = (window << offset) >> (64 - take);
            count += PopCount(bits);
            bit += take;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PopCount(ulong value)
    {
#if NETSTANDARD2_0
        value -= (value >> 1) & 0x5555555555555555UL;
        value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
        value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
        return (int)((value * 0x0101010101010101UL) >> 56);
#else
        return BitOperations.PopCount(value);
#endif
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
    private void WriteAveragedRow(in Span<byte> destRow)
    {
        int totalSamples = _destinationWidth * _components;
        float verticalScale = _sourceToDestinationScale / _accumulatedSourceRowCount;
        UintBitWriter writer = new(destRow);

        for (int sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
        {
            byte value = _destinationRowAccumulators[sampleIndex].GetAverage(verticalScale);
            writer.Write8Bits(value);
        }

        writer.Flush();
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
        public long Sum;
        public float HorizontalReciprocal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte GetAverage(float verticalScale)
        {
            float average = Sum * HorizontalReciprocal * verticalScale;
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
