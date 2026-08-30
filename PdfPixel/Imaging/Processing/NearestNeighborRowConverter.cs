using PdfPixel.Parsing;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Row converter that resamples by nearest neighbor, giving every output sample the source sample
/// whose cell its center falls in. It is the converter for an output grid finer than the source
/// grid along either axis, where a box average has nothing to average over.
/// </summary>
internal sealed class NearestNeighborRowConverter : IRowConverter
{
    private readonly int _components;
    private readonly int _sourceBitsPerComponent;
    private readonly int _destinationWidth;
    private readonly int _destinationHeight;
    private readonly float _scale;

    private readonly uint[] _sourceSamples;
    private readonly int[] _sourceSampleForDestinationSample;
    private readonly int[] _sourceRowForDestinationRow;

    private int _nextDestinationRow;
    private int _readSourceRow = -1;

    public NearestNeighborRowConverter(int components, int sourceBitsPerComponent, int sourceWidth, int destinationWidth, int sourceHeight, int destinationHeight)
    {
        if (sourceBitsPerComponent > 16)
        {
            throw new ArgumentException("Source bits per component must be 16 or less.", nameof(sourceBitsPerComponent));
        }

        _components = components;
        _sourceBitsPerComponent = sourceBitsPerComponent;
        _destinationWidth = destinationWidth;
        _destinationHeight = destinationHeight;

        uint sourceMaximumValue = (1u << sourceBitsPerComponent) - 1u;
        _scale = (sourceMaximumValue == 0) ? 0f : 255f / sourceMaximumValue;

        _sourceSamples = new uint[sourceWidth * components];

        // The column map is expanded per component so a destination sample reads one flat index.
        _sourceSampleForDestinationSample = new int[destinationWidth * components];
        for (int destinationColumn = 0; destinationColumn < destinationWidth; destinationColumn++)
        {
            int sourceColumn = MapToSource(destinationColumn, destinationWidth, sourceWidth);

            for (int component = 0; component < components; component++)
            {
                _sourceSampleForDestinationSample[(destinationColumn * components) + component] = (sourceColumn * components) + component;
            }
        }

        _sourceRowForDestinationRow = new int[destinationHeight];
        for (int destinationRow = 0; destinationRow < destinationHeight; destinationRow++)
        {
            _sourceRowForDestinationRow[destinationRow] = MapToSource(destinationRow, destinationHeight, sourceHeight);
        }
    }

    public bool TryConvertRow(int rowIndex, ReadOnlySpan<byte> sourceRow, int sourceStartBit, Span<byte> destRow)
    {
        // A destination row whose source row has already gone by is taken by the row at hand, so an
        // axis that reduces rather than grows still leaves every destination row written.
        if (_nextDestinationRow >= _destinationHeight || _sourceRowForDestinationRow[_nextDestinationRow] > rowIndex)
        {
            return false;
        }

        if (_readSourceRow != rowIndex)
        {
            ReadSourceRowSamples(sourceRow, sourceStartBit);
            _readSourceRow = rowIndex;
        }

        WriteRow(destRow);
        _nextDestinationRow++;

        return true;
    }

    /// <summary>
    /// Source index whose cell holds the center of the destination cell at <paramref name="destinationIndex"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MapToSource(int destinationIndex, int destinationCount, int sourceCount)
    {
        var sourceIndex = (int)((destinationIndex + 0.5f) * sourceCount / destinationCount);

        return Math.Min(sourceIndex, sourceCount - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadSourceRowSamples(in ReadOnlySpan<byte> sourceRow, int sourceStartBit)
    {
        UintBitReaderFixedLength reader = new(sourceRow, _sourceBitsPerComponent, sourceStartBit);

        for (int sampleIndex = 0; sampleIndex < _sourceSamples.Length; sampleIndex++)
        {
            _sourceSamples[sampleIndex] = reader.Read();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteRow(in Span<byte> destRow)
    {
        int totalSamples = _destinationWidth * _components;
        ref int sourceSampleReference = ref _sourceSampleForDestinationSample[0];
        ref uint sourceSamplesReference = ref _sourceSamples[0];
        UintBitWriter writer = new(destRow);

        for (int sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
        {
            uint sample = Unsafe.Add(ref sourceSamplesReference, Unsafe.Add(ref sourceSampleReference, sampleIndex));
            writer.Write8Bits((byte)(sample * _scale));
        }

        writer.Flush();
    }
}
