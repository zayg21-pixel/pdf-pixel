using PdfPixel.Parsing;
using System;
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
    private readonly int _srcWidth;
    private readonly int _dstWidth;
    private readonly int _srcHeight;
    private readonly int _dstHeight;

    private readonly float _scaleX;
    private readonly float _scaleY;
    private readonly float _inverseScaleY;

    private readonly Accumulator[] _destRowAccumulators;
    private readonly byte[] _sourceSamples;

    private readonly Range[] _srcSampleRangeForDestSample;

    private int _nextSrcRowToRead;
    private int _nextDestRowToWrite;

    private readonly uint _sourceMaxValue;
    private readonly float _sourceToDestinationScale;

    public AveragingDownsampleRowConverter(int components, int sourceBitsPerComponent, int srcWidth, int dstWidth, int srcHeight, int dstHeight)
    {
        if (dstWidth > srcWidth || dstHeight > srcHeight)
        {
            throw new NotSupportedException("Averaging downsample row converter only supports downsampling. Upsampling is not allowed.");
        }

        if (sourceBitsPerComponent != 1 && sourceBitsPerComponent != 2 && sourceBitsPerComponent != 4 && sourceBitsPerComponent != 8 && sourceBitsPerComponent != 16)
        {
            throw new ArgumentException("Source bits per component must be 1, 2, 4, 8, or 16.", nameof(sourceBitsPerComponent));
        }

        _components = components;
        _sourceBitsPerComponent = sourceBitsPerComponent;
        _srcWidth = srcWidth;
        _dstWidth = dstWidth;
        _srcHeight = srcHeight;
        _dstHeight = dstHeight;

        _scaleX = (float)srcWidth / dstWidth;
        _scaleY = (float)srcHeight / dstHeight;
        _inverseScaleY = 1.0f / _scaleY;

        _sourceMaxValue = sourceBitsPerComponent == 16 ? 65535u : ((1u << sourceBitsPerComponent) - 1u);
        _sourceToDestinationScale = _sourceMaxValue == 0 ? 0.0f : 255.0f / _sourceMaxValue;

        int totalDestSamples = _dstWidth * components;
        _destRowAccumulators = new Accumulator[totalDestSamples];
        _sourceSamples = new byte[_srcWidth * components];

        // Precompute flat sample start/end indices for each destination sample (component)
        _srcSampleRangeForDestSample = new Range[_dstWidth * components];
        for (int dstCol = 0; dstCol < _dstWidth; dstCol++)
        {
            float srcStartF = dstCol * _scaleX;
            float srcEndF = (dstCol + 1) * _scaleX;
            int srcStart = (int)Math.Floor(srcStartF);
            int srcEnd = (int)Math.Ceiling(srcEndF);
            if (srcStart < 0) srcStart = 0;
            if (srcEnd > _srcWidth) srcEnd = _srcWidth;
            for (int c = 0; c < components; c++)
            {
                int dstIndex = dstCol * components + c;
                int flatStart = srcStart * components + c;
                int flatEnd = srcEnd * components + c;
                _srcSampleRangeForDestSample[dstIndex] = new Range(flatStart, flatEnd);
            }
        }

        _nextSrcRowToRead = 0;
        _nextDestRowToWrite = 0;
    }

    public int BitsPerComponent => 8;

    public bool TryConvertRow(int rowIndex, ReadOnlySpan<byte> sourceRow, Span<byte> destRow)
    {
        if (_nextDestRowToWrite >= _dstHeight)
        {
            return false;
        }

        if (rowIndex != _nextSrcRowToRead)
        {
            return false;
        }

        ReadSourceRowSamples(sourceRow);

        int destRowForThisSource = GetDestinationRow(rowIndex);

        if (destRowForThisSource > _nextDestRowToWrite)
        {
            if (_nextDestRowToWrite < _dstHeight)
            {
                WriteAveragedRow(destRow);
                ResetAccumulators();
                _nextDestRowToWrite++;
            }
        }

        AccumulateSourceRow();
        _nextSrcRowToRead++;

        if (destRowForThisSource == _nextDestRowToWrite && IsLastSourceRowForDestRow(rowIndex, _nextDestRowToWrite))
        {
            WriteAveragedRow(destRow);
            ResetAccumulators();
            _nextDestRowToWrite++;
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadSourceRowSamples(ReadOnlySpan<byte> sourceRow)
    {
        int count = _srcWidth * _components;

        if (_sourceBitsPerComponent == 8)
        {
            sourceRow.CopyTo(_sourceSamples);
            return;
        }

        // Fallback
        var reader = new UintBitReaderFixedLength(sourceRow, _sourceBitsPerComponent);
        for (int i = 0; i < count; i++)
        {
            _sourceSamples[i] = (byte)reader.Read();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetDestinationRow(int srcRow)
    {
        return (int)((srcRow + 0.5f) * _inverseScaleY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsLastSourceRowForDestRow(int srcRow, int destRow)
    {
        if (destRow >= _dstHeight - 1)
        {
            return srcRow >= _srcHeight - 1;
        }

        int nextDestRow = destRow + 1;
        int firstSrcRowForNextDest = (int)(nextDestRow * _scaleY);
        return srcRow >= firstSrcRowForNextDest - 1;
    }

    private void AccumulateSourceRow()
    {
        int totalDestSamples = _dstWidth * _components;
        ref var destAccumulatorsRef = ref _destRowAccumulators[0];
        ReadOnlySpan<byte> sourceSamplesSpan = _sourceSamples.AsSpan();
        ReadOnlySpan<Range> sampleRangesSpan = _srcSampleRangeForDestSample.AsSpan();

        for (int dstIndex = 0; dstIndex < totalDestSamples; dstIndex++)
        {
            var range = sampleRangesSpan[dstIndex];
            long sum = 0;
            int count = 0;

            for (int srcIndex = range.Start; srcIndex < range.End; srcIndex += _components)
            {
                uint value = sourceSamplesSpan[srcIndex];
                if (_sourceBitsPerComponent != 8)
                {
                    value = (uint)((value * _sourceToDestinationScale) + 0.5f);
                }
                sum += value;
                
                count++;
            }

            destAccumulatorsRef.Add(sum, count);
            destAccumulatorsRef = ref Unsafe.Add(ref destAccumulatorsRef, 1);
        }
    }

    private void WriteAveragedRow(Span<byte> destRow)
    {
        int totalSamples = _dstWidth * _components;
        var writer = new UintBitWriter(destRow);
        for (int i = 0; i < totalSamples; i++)
        {
            byte value = _destRowAccumulators[i].GetAverage(255);
            writer.Write8Bits(value);
        }
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetAccumulators()
    {
        Array.Clear(_destRowAccumulators, 0, _destRowAccumulators.Length);
    }

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
        public readonly byte GetAverage(uint maxValue)
        {
            if (Count == 0)
            {
                return 0;
            }
            long average = (Sum + (Count >> 1)) / Count;
            if (average < 0)
            {
                average = 0;
            }
            if (average > maxValue)
            {
                average = maxValue;
            }
            return (byte)average;
        }
    }
    private readonly struct Range
    {
        public int Start { get; }
        public int End { get; }
        public Range(int start, int end)
        {
            Start = start;
            End = end;
        }
    }
}

