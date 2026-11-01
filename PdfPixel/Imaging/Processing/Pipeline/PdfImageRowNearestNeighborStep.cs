using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Resamples float samples by nearest neighbor. Every destination sample takes the source sample whose
/// cell its center falls in.
/// </summary>
public sealed class PdfImageRowNearestNeighborStep : IPdfImageRowProcessingStep
{
    private readonly int _components;
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private readonly int _destinationHeight;

    private readonly int[] _sourceSampleForDestinationSample;
    private readonly int[] _sourceRowForDestinationRow;
    private readonly float[] _buffer;

    private int _nextDestinationRow;
    private int _writtenSourceRow = -1;

    /// <summary>
    /// Initializes a step resampling a source grid onto a destination grid.
    /// </summary>
    /// <param name="components">Number of channels in each pixel.</param>
    /// <param name="sourceWidth">Number of pixels in each source row.</param>
    /// <param name="destinationWidth">Number of pixels in each destination row.</param>
    /// <param name="sourceHeight">Number of source rows.</param>
    /// <param name="destinationHeight">Number of destination rows.</param>
    public PdfImageRowNearestNeighborStep(int components, int sourceWidth, int destinationWidth, int sourceHeight, int destinationHeight)
    {
        _components = components;
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _destinationHeight = destinationHeight;
        _buffer = new float[destinationWidth * components];

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

        // A destination row whose source row has already gone by is taken by the row at hand, so an
        // axis that reduces rather than grows still leaves every destination row written.
        if (HasDestinationRowFor(inputRowIndex))
        {
            if (_writtenSourceRow != inputRowIndex)
            {
                ReadOnlySpan<float> samples = MemoryMarshal.Cast<byte, float>(input);

                if (samples.Length < _sourceWidth * _components)
                {
                    throw new ArgumentException("The input row holds fewer samples than the source grid.", nameof(input));
                }

                WriteRow(samples);
                _writtenSourceRow = inputRowIndex;
            }

            int destinationRow = _nextDestinationRow;
            _nextDestinationRow++;

            return new PdfImageRow(MemoryMarshal.AsBytes(_buffer.AsSpan()), destinationRow, HasDestinationRowFor(inputRowIndex));
        }
        else
        {
            return PdfImageRow.Empty;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasDestinationRowFor(int sourceRow)
        => _nextDestinationRow < _destinationHeight && _sourceRowForDestinationRow[_nextDestinationRow] <= sourceRow;

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void WriteRow(in ReadOnlySpan<float> samples)
    {
        ref int sourceSampleReference = ref _sourceSampleForDestinationSample[0];
        ref float sourceReference = ref MemoryMarshal.GetReference(samples);
        ref float destinationReference = ref _buffer[0];

        for (int sampleIndex = 0; sampleIndex < _buffer.Length; sampleIndex++)
        {
            Unsafe.Add(ref destinationReference, sampleIndex) = Unsafe.Add(ref sourceReference, Unsafe.Add(ref sourceSampleReference, sampleIndex));
        }
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
}
