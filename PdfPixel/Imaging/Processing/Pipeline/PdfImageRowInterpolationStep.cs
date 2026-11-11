using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Interpolates float samples along the axes that grow, by its <see cref="PdfImageRowInterpolationMode"/>: every
/// destination sample blends at most two source samples. Along an axis that does not grow the input already holds the
/// destination samples, as a <see cref="PdfImageRowResizeStep"/> before it writes them; along one that grows it holds the
/// source samples of
/// <see cref="PdfImageRowAxisMapping.GetInterpolationRange"/>, as kept by
/// <see cref="PdfImageRowAxisMapping.GetSourceSamplesMapping"/>.
/// </summary>
public sealed class PdfImageRowInterpolationStep : IPdfImageRowProcessingStep
{
    private readonly int _components;
    private readonly int _inputWidth;
    private readonly int _inputHeight;
    private readonly int _destinationWidth;
    private readonly int _destinationHeight;
    private readonly int[] _columnFirstOffsets;
    private readonly int[] _columnSecondOffsets;
    private readonly float[] _columnWeights;
    private readonly int[] _rowFirst;
    private readonly int[] _rowSecond;
    private readonly float[] _rowWeights;
    private readonly float[] _previousRow;
    private readonly float[] _currentRow;

    private int _currentInputRow;
    private int _nextDestinationRow;

    /// <summary>
    /// Initializes a step interpolating float samples along the axes of <paramref name="horizontal"/> and
    /// <paramref name="vertical"/> that grow.
    /// </summary>
    /// <param name="components">Number of components in each pixel.</param>
    /// <param name="mode">How the axes that grow are interpolated.</param>
    /// <param name="horizontal">How destination columns map onto source columns.</param>
    /// <param name="vertical">How destination rows map onto source rows.</param>
    public PdfImageRowInterpolationStep(
        int components,
        PdfImageRowInterpolationMode mode,
        in PdfImageRowAxisMapping horizontal,
        in PdfImageRowAxisMapping vertical)
    {
        if (components <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(components), "Component count must be positive.");
        }

        _components = components;
        _destinationWidth = horizontal.DestinationCount;
        _destinationHeight = vertical.DestinationCount;
        _columnFirstOffsets = new int[_destinationWidth];
        _columnSecondOffsets = new int[_destinationWidth];
        _columnWeights = new float[_destinationWidth];
        _rowFirst = new int[_destinationHeight];
        _rowSecond = new int[_destinationHeight];
        _rowWeights = new float[_destinationHeight];
        _inputWidth = SetTaps(mode, horizontal, _columnFirstOffsets, _columnSecondOffsets, _columnWeights);
        _inputHeight = SetTaps(mode, vertical, _rowFirst, _rowSecond, _rowWeights);

        for (int column = 0; column < _destinationWidth; column++)
        {
            _columnFirstOffsets[column] *= components;
            _columnSecondOffsets[column] *= components;
        }

        _previousRow = new float[_destinationWidth * components];
        _currentRow = new float[_destinationWidth * components];
        _currentInputRow = -1;
    }

    /// <inheritdoc />
    public int GetOutputLength(int inputLength) => _destinationWidth * _components * sizeof(float);

    /// <inheritdoc />
    public PdfImageOutputRow GetOutputRow(int inputRowIndex)
    {
        if (inputRowIndex < 0 || inputRowIndex >= _inputHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(inputRowIndex), "The input row is outside the source grid.");
        }

        if (_nextDestinationRow >= _destinationHeight || _rowSecond[_nextDestinationRow] != inputRowIndex)
        {
            return PdfImageOutputRow.Empty;
        }

        bool hasNextRow = _nextDestinationRow + 1 < _destinationHeight && _rowSecond[_nextDestinationRow + 1] == inputRowIndex;

        return new PdfImageOutputRow(_nextDestinationRow, hasNextRow);
    }

    /// <inheritdoc />
    public void Process(ReadOnlySpan<byte> input, Span<byte> output, int inputRowIndex)
    {
        if (inputRowIndex < 0 || inputRowIndex >= _inputHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(inputRowIndex), "The input row is outside the source grid.");
        }

        if (!input.IsEmpty && inputRowIndex != _currentInputRow)
        {
            if (input.Length < _inputWidth * _components * sizeof(float))
            {
                throw new ArgumentException("The input row holds fewer samples than the source grid.", nameof(input));
            }

            _currentRow.AsSpan().CopyTo(_previousRow);
            InterpolateColumns(MemoryMarshal.Cast<byte, float>(input), _currentRow);
            _currentInputRow = inputRowIndex;
        }

        if (_nextDestinationRow < _destinationHeight && _rowSecond[_nextDestinationRow] == inputRowIndex)
        {
            InterpolateRows(_nextDestinationRow, MemoryMarshal.Cast<byte, float>(output));
            _nextDestinationRow++;
        }
    }

    /// <inheritdoc />
    public void Process(ReadOnlySpan<byte> input, Span<byte> output)
        => throw new NotSupportedException("The interpolation step maps rows by their index.");

    /// <summary>
    /// Sets the two input samples each destination sample blends and the weight of the second, and returns the number
    /// of input samples along the axis.
    /// </summary>
    private static int SetTaps(PdfImageRowInterpolationMode mode, in PdfImageRowAxisMapping mapping, int[] first, int[] second, float[] weights)
    {
        if (!mapping.Grows)
        {
            return SetKeptTaps(mapping.DestinationCount, first, second);
        }
        else if (mode == PdfImageRowInterpolationMode.Area)
        {
            return SetAreaTaps(mapping, first, second, weights);
        }
        else
        {
            return SetLinearTaps(mapping, first, second, weights);
        }
    }

    /// <summary>
    /// Sets every destination sample to the input sample at its own index, as along an axis the input already holds
    /// the destination samples of.
    /// </summary>
    private static int SetKeptTaps(int count, int[] first, int[] second)
    {
        for (int destinationIndex = 0; destinationIndex < count; destinationIndex++)
        {
            first[destinationIndex] = destinationIndex;
            second[destinationIndex] = destinationIndex;
        }

        return count;
    }

    /// <summary>
    /// Sets every destination sample to the two source samples around its center, the source edge samples beyond the
    /// outermost centers.
    /// </summary>
    private static int SetLinearTaps(in PdfImageRowAxisMapping mapping, int[] first, int[] second, float[] weights)
    {
        PdfImageRowSampleRange range = mapping.GetInterpolationRange();
        int lastInput = range.Count - 1;

        for (int destinationIndex = 0; destinationIndex < mapping.DestinationCount; destinationIndex++)
        {
            float position = mapping.GetCenter(destinationIndex) - 0.5f - range.Start;
            var lower = (int)Math.Floor(position);

            if (lower < 0)
            {
                first[destinationIndex] = 0;
                second[destinationIndex] = 0;
            }
            else if (lower >= lastInput)
            {
                first[destinationIndex] = lastInput;
                second[destinationIndex] = lastInput;
            }
            else
            {
                first[destinationIndex] = lower;
                second[destinationIndex] = lower + 1;
                weights[destinationIndex] = position - lower;
            }
        }

        return range.Count;
    }

    /// <summary>
    /// Sets every destination sample to the source samples its cell covers: the one it lies in, or the two on either side
    /// of the sample boundary it crosses, weighted by the share of the cell each covers; the source edge samples beyond
    /// the source.
    /// </summary>
    private static int SetAreaTaps(in PdfImageRowAxisMapping mapping, int[] first, int[] second, float[] weights)
    {
        PdfImageRowSampleRange range = mapping.GetInterpolationRange();
        int lastInput = range.Count - 1;
        float halfStep = mapping.Step * 0.5f;

        for (int destinationIndex = 0; destinationIndex < mapping.DestinationCount; destinationIndex++)
        {
            float center = mapping.GetCenter(destinationIndex) - range.Start;
            float cellEnd = center + halfStep;
            var boundary = (int)Math.Floor(cellEnd);

            if (boundary > center - halfStep && boundary >= 1 && boundary <= lastInput)
            {
                first[destinationIndex] = boundary - 1;
                second[destinationIndex] = boundary;
                weights[destinationIndex] = (cellEnd - boundary) / mapping.Step;
            }
            else
            {
                int sample = Math.Max(0, Math.Min(lastInput, (int)Math.Floor(center)));

                first[destinationIndex] = sample;
                second[destinationIndex] = sample;
            }
        }

        return range.Count;
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void InterpolateColumns(in ReadOnlySpan<float> input, in Span<float> destination)
    {
        int components = _components;
        ref float source = ref MemoryMarshal.GetReference(input);
        ref float value = ref MemoryMarshal.GetReference(destination);
        ref int firstOffset = ref _columnFirstOffsets[0];
        ref int secondOffset = ref _columnSecondOffsets[0];
        ref float columnWeight = ref _columnWeights[0];

        for (int column = 0; column < _destinationWidth; column++)
        {
            ref float first = ref Unsafe.Add(ref source, Unsafe.Add(ref firstOffset, column));
            ref float second = ref Unsafe.Add(ref source, Unsafe.Add(ref secondOffset, column));
            float weight = Unsafe.Add(ref columnWeight, column);

            for (int component = 0; component < components; component++)
            {
                float firstValue = Unsafe.Add(ref first, component);

                Unsafe.Add(ref value, component) = firstValue + (weight * (Unsafe.Add(ref second, component) - firstValue));
            }

            value = ref Unsafe.Add(ref value, components);
        }
    }

    /// <summary>
    /// Writes destination row <paramref name="destinationRow"/>, blending the kept rows it lies between.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void InterpolateRows(int destinationRow, in Span<float> destination)
    {
        float[] firstRow = (_rowFirst[destinationRow] == _currentInputRow) ? _currentRow : _previousRow;
        float weight = _rowWeights[destinationRow];
        ref float first = ref firstRow[0];
        ref float second = ref _currentRow[0];
        ref float value = ref MemoryMarshal.GetReference(destination);

        for (int index = 0; index < _currentRow.Length; index++)
        {
            float firstValue = Unsafe.Add(ref first, index);

            Unsafe.Add(ref value, index) = firstValue + (weight * (Unsafe.Add(ref second, index) - firstValue));
        }
    }
}
