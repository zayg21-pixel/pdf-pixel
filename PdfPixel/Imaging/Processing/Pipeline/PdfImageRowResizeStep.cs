using PdfPixel.Geometry;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Resizes samples onto a destination grid, giving float samples: every destination sample of a component is the
/// mean of that component's source samples in its footprint along each axis.
/// Along an axis that shrinks the footprint is the source samples whose centers lie in the destination sample's cell,
/// along one that grows the sample under its center. Packed integer samples are summed as codes and divided by the
/// largest code, giving values in the range 0 to 1; float samples are averaged as they are.
/// </summary>
public sealed class PdfImageRowResizeStep : IPdfImageRowProcessingStep
{
    private const int ByteBitsPerComponent = 8;
    private const int FloatBitsPerComponent = sizeof(float) * 8;

    private readonly int _components;
    private readonly PdfImageSampleFormat _format;
    private readonly int _bitsPerComponent;
    private readonly int _destinationWidth;
    private readonly int _destinationHeight;
    private readonly long _requiredInputBits;
    private readonly int _sourceHeight;
    private readonly int[] _columnStarts;
    private readonly int[] _columnEnds;
    private readonly double[] _columnScales;
    private readonly int[] _rowStarts;
    private readonly int[] _rowEnds;
    private readonly double[] _sums;

    private int _nextDestinationRow;

    /// <summary>
    /// Initializes a step resizing source samples onto destination samples.
    /// </summary>
    /// <param name="components">Number of components in each pixel.</param>
    /// <param name="format">How the source samples are stored.</param>
    /// <param name="bitsPerComponent">Bit depth of each source sample: 1 to 16 for integer samples, 32 for floats.</param>
    /// <param name="horizontal">How destination columns map onto source columns.</param>
    /// <param name="vertical">How destination rows map onto source rows.</param>
    public PdfImageRowResizeStep(
        int components,
        PdfImageSampleFormat format,
        int bitsPerComponent,
        in PdfImageRowAxisMapping horizontal,
        in PdfImageRowAxisMapping vertical)
    {
        if (components <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(components), "Component count must be positive.");
        }

        if (format == PdfImageSampleFormat.Integer && (bitsPerComponent < 1 || bitsPerComponent > 16))
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Integer samples must have 1 to 16 bits per component.");
        }

        if (format == PdfImageSampleFormat.Float && bitsPerComponent != FloatBitsPerComponent)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Float samples must have 32 bits per component.");
        }

        int destinationWidth = horizontal.DestinationCount;
        int destinationHeight = vertical.DestinationCount;
        double largestValue = (format == PdfImageSampleFormat.Integer) ? (1L << bitsPerComponent) - 1 : 1;

        _components = components;
        _format = format;
        _bitsPerComponent = bitsPerComponent;
        _destinationWidth = destinationWidth;
        _destinationHeight = destinationHeight;
        _columnStarts = new int[destinationWidth];
        _columnEnds = new int[destinationWidth];
        _columnScales = new double[destinationWidth];
        _rowStarts = new int[destinationHeight];
        _rowEnds = new int[destinationHeight];
        _sums = new double[destinationWidth * components];

        int columnStart = horizontal.SourceCount;
        int columnEnd = 0;

        for (int destinationColumn = 0; destinationColumn < destinationWidth; destinationColumn++)
        {
            PdfImageRowSampleRange footprint = horizontal.GetFootprint(destinationColumn);
            _columnStarts[destinationColumn] = footprint.Start;
            _columnEnds[destinationColumn] = footprint.End;
            _columnScales[destinationColumn] = 1 / (footprint.Count * largestValue);
            columnStart = Math.Min(columnStart, footprint.Start);
            columnEnd = Math.Max(columnEnd, footprint.End);
        }

        for (int destinationColumn = 0; destinationColumn < destinationWidth; destinationColumn++)
        {
            _columnStarts[destinationColumn] = (_columnStarts[destinationColumn] - columnStart) * components;
            _columnEnds[destinationColumn] = (_columnEnds[destinationColumn] - columnStart) * components;
        }

        int rowStart = vertical.GetFootprint(0).Start;
        int rowEnd = vertical.GetFootprint(destinationHeight - 1).End;

        // Rows are read once, in order: a footprint that rounding makes overlap the one before it starts where that one
        // ends, and one that lies within it repeats it.
        for (int destinationRow = 0; destinationRow < destinationHeight; destinationRow++)
        {
            PdfImageRowSampleRange footprint = vertical.GetFootprint(destinationRow);
            int start = footprint.Start - rowStart;
            int end = footprint.End - rowStart;

            if (destinationRow > 0 && start < _rowEnds[destinationRow - 1] && !IsSameFootprint(destinationRow - 1, start, end))
            {
                start = _rowEnds[destinationRow - 1];

                if (start >= end)
                {
                    start = _rowStarts[destinationRow - 1];
                    end = _rowEnds[destinationRow - 1];
                }
            }

            _rowStarts[destinationRow] = start;
            _rowEnds[destinationRow] = end;
        }

        _requiredInputBits = (long)(columnEnd - columnStart) * components * bitsPerComponent;
        _sourceHeight = rowEnd - rowStart;
        SourceWindow = new PdfIntegerRectangle(columnStart, rowStart, columnEnd, rowEnd);
    }

    /// <summary>
    /// Gets the source samples the step reads, in columns and rows; input rows hold its columns and are numbered from
    /// its top.
    /// </summary>
    public PdfIntegerRectangle SourceWindow { get; }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => _sums.Length * sizeof(float);

    /// <inheritdoc />
    public PdfImageOutputRow GetOutputRow(int inputRowIndex)
    {
        if (inputRowIndex < 0 || inputRowIndex >= _sourceHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(inputRowIndex), "The input row is outside the source grid.");
        }

        if (_nextDestinationRow >= _destinationHeight || _rowEnds[_nextDestinationRow] - 1 != inputRowIndex)
        {
            return PdfImageOutputRow.Empty;
        }

        return new PdfImageOutputRow(_nextDestinationRow, IsRepeated(_nextDestinationRow + 1));
    }

    /// <inheritdoc />
    public void Process(ReadOnlySpan<byte> input, Span<byte> output, int inputRowIndex)
    {
        if (inputRowIndex < 0 || inputRowIndex >= _sourceHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(inputRowIndex), "The input row is outside the source grid.");
        }

        if (_nextDestinationRow >= _destinationHeight)
        {
            return;
        }

        bool writesRow = input.IsEmpty;
        int rowStart = _rowStarts[_nextDestinationRow];
        int rowEnd = _rowEnds[_nextDestinationRow];

        if (!input.IsEmpty && inputRowIndex >= rowStart && inputRowIndex < rowEnd)
        {
            if ((long)input.Length * 8 < _requiredInputBits)
            {
                throw new ArgumentException("The input row holds fewer samples than the source grid.", nameof(input));
            }

            if (inputRowIndex == rowStart)
            {
                _sums.AsSpan().Clear();
            }

            AccumulateRow(ref MemoryMarshal.GetReference(input));
            writesRow = inputRowIndex == rowEnd - 1;
        }

        if (writesRow)
        {
            WriteAverages(MemoryMarshal.Cast<byte, float>(output), rowEnd - rowStart);
            _nextDestinationRow++;
        }
    }

    /// <inheritdoc />
    public void Process(ReadOnlySpan<byte> input, Span<byte> output)
        => throw new NotSupportedException("The resize step maps rows by their index.");

    /// <summary>
    /// Whether destination row <paramref name="destinationRow"/> has the same footprint as the row before it, so that
    /// it repeats it.
    /// </summary>
    private bool IsRepeated(int destinationRow)
    {
        return destinationRow < _destinationHeight
            && IsSameFootprint(destinationRow - 1, _rowStarts[destinationRow], _rowEnds[destinationRow]);
    }

    /// <summary>
    /// Whether destination row <paramref name="destinationRow"/> has the footprint from source row
    /// <paramref name="start"/> up to <paramref name="end"/>.
    /// </summary>
    private bool IsSameFootprint(int destinationRow, int start, int end)
        => _rowStarts[destinationRow] == start && _rowEnds[destinationRow] == end;

    /// <summary>
    /// Adds the samples of the source row at <paramref name="row"/> to the sums of every destination sample.
    /// </summary>
    private void AccumulateRow(ref byte row)
    {
        if (_format == PdfImageSampleFormat.Float)
        {
            AccumulateFloats(ref Unsafe.As<byte, float>(ref row));
        }
        else if (_bitsPerComponent == 1 && _components == 1)
        {
            AccumulateBits(ref row);
        }
        else if (_bitsPerComponent == ByteBitsPerComponent)
        {
            AccumulateBytes(ref row);
        }
        else
        {
            AccumulateCodes(ref row);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AccumulateFloats(ref float row)
    {
        int components = _components;
        ref double sum = ref _sums[0];
        ref int columnStart = ref _columnStarts[0];
        ref int columnEnd = ref _columnEnds[0];

        for (int column = 0; column < _destinationWidth; column++)
        {
            int sampleEnd = Unsafe.Add(ref columnEnd, column);

            for (int sample = Unsafe.Add(ref columnStart, column); sample < sampleEnd; sample += components)
            {
                for (int component = 0; component < components; component++)
                {
                    Unsafe.Add(ref sum, component) += Unsafe.Add(ref row, sample + component);
                }
            }

            sum = ref Unsafe.Add(ref sum, components);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AccumulateBits(ref byte row)
    {
        ref double sum = ref _sums[0];
        ref int columnStart = ref _columnStarts[0];
        ref int columnEnd = ref _columnEnds[0];

        for (int column = 0; column < _destinationWidth; column++)
        {
            Unsafe.Add(ref sum, column) += PdfPackedSamples.CountSetBits(ref row, Unsafe.Add(ref columnStart, column), Unsafe.Add(ref columnEnd, column));
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AccumulateBytes(ref byte row)
    {
        int components = _components;
        ref double sum = ref _sums[0];
        ref int columnStart = ref _columnStarts[0];
        ref int columnEnd = ref _columnEnds[0];

        for (int column = 0; column < _destinationWidth; column++)
        {
            int sampleEnd = Unsafe.Add(ref columnEnd, column);

            for (int sample = Unsafe.Add(ref columnStart, column); sample < sampleEnd; sample += components)
            {
                for (int component = 0; component < components; component++)
                {
                    Unsafe.Add(ref sum, component) += Unsafe.Add(ref row, sample + component);
                }
            }

            sum = ref Unsafe.Add(ref sum, components);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AccumulateCodes(ref byte row)
    {
        int components = _components;
        int bitsPerComponent = _bitsPerComponent;
        ref double sum = ref _sums[0];
        ref int columnStart = ref _columnStarts[0];
        ref int columnEnd = ref _columnEnds[0];

        for (int column = 0; column < _destinationWidth; column++)
        {
            int sampleEnd = Unsafe.Add(ref columnEnd, column);

            for (int sample = Unsafe.Add(ref columnStart, column); sample < sampleEnd; sample += components)
            {
                for (int component = 0; component < components; component++)
                {
                    Unsafe.Add(ref sum, component) += PdfPackedSamples.Read(ref row, sample + component, bitsPerComponent);
                }
            }

            sum = ref Unsafe.Add(ref sum, components);
        }
    }

    /// <summary>
    /// Writes every destination sample: its sum over <paramref name="rowCount"/> source rows divided by its footprint's
    /// largest possible sum.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void WriteAverages(in Span<float> destination, int rowCount)
    {
        int components = _components;
        double rowScale = 1.0 / rowCount;
        ref double sum = ref _sums[0];
        ref double columnScale = ref _columnScales[0];
        ref float value = ref MemoryMarshal.GetReference(destination);

        for (int column = 0; column < _destinationWidth; column++)
        {
            double scale = Unsafe.Add(ref columnScale, column) * rowScale;

            for (int component = 0; component < components; component++)
            {
                Unsafe.Add(ref value, component) = (float)(Unsafe.Add(ref sum, component) * scale);
            }

            sum = ref Unsafe.Add(ref sum, components);
            value = ref Unsafe.Add(ref value, components);
        }
    }
}
