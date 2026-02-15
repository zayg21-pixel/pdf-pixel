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
    private readonly int _destinationBitsPerComponent;
    private readonly int _srcWidth;
    private readonly int _dstWidth;
    private readonly int _srcHeight;
    private readonly int _dstHeight;

    private readonly float _scaleX;
    private readonly float _scaleY;

    private readonly long[] _destRowAccumulators;
    private readonly int[] _destRowCounts;
    private readonly uint[] _sourceSamples;

    private int _nextSrcRowToRead;
    private int _nextDestRowToWrite;

    private readonly uint _sourceMaxValue;
    private readonly uint _destinationMaxValue;
    private readonly float _sourceToDestinationScale;

    public int BitsPerComponent
    {
        get
        {
            return _destinationBitsPerComponent;
        }
    }

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
        _destinationBitsPerComponent = sourceBitsPerComponent == 16 ? 16 : 8;
        _srcWidth = srcWidth;
        _dstWidth = dstWidth;
        _srcHeight = srcHeight;
        _dstHeight = dstHeight;

        _scaleX = (float)srcWidth / dstWidth;
        _scaleY = (float)srcHeight / dstHeight;

        _sourceMaxValue = sourceBitsPerComponent == 16 ? 65535u : ((1u << sourceBitsPerComponent) - 1u);
        _destinationMaxValue = _destinationBitsPerComponent == 16 ? 65535u : 255u;
        _sourceToDestinationScale = _sourceMaxValue == 0 ? 0.0f : (float)_destinationMaxValue / _sourceMaxValue;

        int totalDestSamples = _dstWidth * components;
        _destRowAccumulators = new long[totalDestSamples];
        _destRowCounts = new int[totalDestSamples];
        _sourceSamples = new uint[_srcWidth * components];

        _nextSrcRowToRead = 0;
        _nextDestRowToWrite = 0;
    }

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

        AccumulateSourceRow(rowIndex);
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
        var reader = new UintBitReader(sourceRow);
        int count = _srcWidth * _components;
        for (int i = 0; i < count; i++)
        {
            _sourceSamples[i] = reader.ReadBits(_sourceBitsPerComponent);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetDestinationRow(int srcRow)
    {
        return (int)((srcRow + 0.5f) / _scaleY);
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

    private void AccumulateSourceRow(int srcRow)
    {
        for (int sx = 0; sx < _srcWidth; sx++)
        {
            int dx = GetDestinationColumn(sx);
            if (dx < 0 || dx >= _dstWidth)
            {
                continue;
            }

            int srcBaseIndex = sx * _components;
            int dstBaseIndex = dx * _components;

            for (int c = 0; c < _components; c++)
            {
                int srcIndex = srcBaseIndex + c;
                int dstIndex = dstBaseIndex + c;

                uint value = _sourceSamples[srcIndex];
                if (_destinationBitsPerComponent != _sourceBitsPerComponent)
                {
                    value = (uint)((value * _sourceToDestinationScale) + 0.5f);
                }

                _destRowAccumulators[dstIndex] += value;
                _destRowCounts[dstIndex]++;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetDestinationColumn(int srcCol)
    {
        return (int)((srcCol + 0.5f) / _scaleX);
    }

    private void WriteAveragedRow(Span<byte> destRow)
    {
        int totalSamples = _dstWidth * _components;
        var writer = new UintBitWriter(destRow);
        for (int i = 0; i < totalSamples; i++)
        {
            int count = _destRowCounts[i];
            uint value;

            if (count > 0)
            {
                long average = (_destRowAccumulators[i] + (count >> 1)) / count;

                if (average < 0)
                {
                    average = 0;
                }
                if (average > _destinationMaxValue)
                {
                    average = _destinationMaxValue;
                }

                value = (uint)average;
            }
            else
            {
                value = 0;
            }

            if (_destinationBitsPerComponent == 8)
            {
                writer.Write8Bits((byte)value);
            }
            else if (_destinationBitsPerComponent == 16)
            {
                writer.Write16Bits((ushort)value);
            }
            else
            {
                writer.WriteBits(_destinationBitsPerComponent, value);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetAccumulators()
    {
        Array.Clear(_destRowAccumulators, 0, _destRowAccumulators.Length);
        Array.Clear(_destRowCounts, 0, _destRowCounts.Length);
    }
}
