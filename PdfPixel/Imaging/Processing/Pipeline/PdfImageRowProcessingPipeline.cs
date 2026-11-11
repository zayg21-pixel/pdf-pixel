using System;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Reads input rows from an <see cref="IPdfImageRowSource"/>, runs them through a chain of
/// <see cref="IPdfImageRowProcessingStep"/> and writes the rows they produce into an <see cref="IPdfImageRowTarget"/>,
/// only as far as asked. Every step between the input row and the target works in one buffer, sized to the longest row
/// any step writes, its output starting where its input does.
/// </summary>
public sealed class PdfImageRowProcessingPipeline
{
    private const int NoPendingStep = -1;

    private readonly IPdfImageRowSource _source;
    private readonly IPdfImageRowProcessingStep[] _steps;
    private readonly IPdfImageRowTarget _target;
    private readonly byte[] _buffer;
    private readonly int[] _outputLengths;
    private bool _hasOutputLengths;
    private int _nextInputRow;
    private int _pendingStep = NoPendingStep;
    private int _pendingRowIndex;

    /// <summary>
    /// Initializes a pipeline reading <paramref name="source"/>, running <paramref name="steps"/> in order and writing
    /// into <paramref name="target"/>, its steps working in <paramref name="buffer"/>.
    /// </summary>
    /// <param name="source">Rows the input rows are read from.</param>
    /// <param name="steps">Steps each row runs through, in order.</param>
    /// <param name="target">Rows the produced rows are written into.</param>
    /// <param name="buffer">Buffer the steps work in, at least <see cref="GetBufferLength"/> bytes long.</param>
    public PdfImageRowProcessingPipeline(IPdfImageRowSource source, IPdfImageRowProcessingStep[] steps, IPdfImageRowTarget target, byte[] buffer)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

        if (steps.Length == 0)
        {
            throw new ArgumentException("At least one step is required.", nameof(steps));
        }

        _outputLengths = new int[steps.Length];
    }

    /// <summary>
    /// Gets the number of rows written.
    /// </summary>
    public int WrittenRowCount { get; private set; }

    /// <summary>
    /// Returns the number of bytes the buffer of a pipeline running <paramref name="steps"/> over input rows of
    /// <paramref name="inputRowLength"/> bytes needs: the longest row any step writes.
    /// </summary>
    /// <param name="steps">Steps each row runs through, in order.</param>
    /// <param name="inputRowLength">Number of bytes of an input row.</param>
    public static int GetBufferLength(IPdfImageRowProcessingStep[] steps, int inputRowLength)
    {
        if (steps == null)
        {
            throw new ArgumentNullException(nameof(steps));
        }

        int length = inputRowLength;
        int longest = 0;

        foreach (IPdfImageRowProcessingStep step in steps)
        {
            length = step.GetOutputLength(length);
            longest = Math.Max(longest, length);
        }

        return longest;
    }

    /// <summary>
    /// Reads input rows and writes output rows until output row <paramref name="rowIndex"/> is written, or the input
    /// ends; does nothing when it is already written.
    /// </summary>
    /// <param name="rowIndex">Index of the output row to write.</param>
    public void ProcessUntilRow(int rowIndex)
    {
        while (WrittenRowCount <= rowIndex)
        {
            if (_pendingStep != NoPendingStep)
            {
                int stepIndex = _pendingStep;
                _pendingStep = NoPendingStep;
                ProcessFrom(stepIndex, ReadOnlySpan<byte>.Empty, _pendingRowIndex);
            }
            else if (_nextInputRow < _source.RowCount)
            {
                ReadOnlySpan<byte> row = _source.ReadRow(_nextInputRow);

                if (!_hasOutputLengths)
                {
                    SetOutputLengths(row.Length);
                }

                ProcessFrom(0, row, _nextInputRow);
                _nextInputRow++;
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>
    /// Runs <paramref name="row"/>, input row <paramref name="rowIndex"/> of step
    /// <paramref name="stepIndex"/>, through the steps from that one until a row is written or no step has one to
    /// write; a step with more rows from its input is kept pending.
    /// </summary>
    private void ProcessFrom(int stepIndex, in ReadOnlySpan<byte> row, int rowIndex)
    {
        ReadOnlySpan<byte> input = row;
        int inputRowIndex = rowIndex;

        while (true)
        {
            IPdfImageRowProcessingStep step = _steps[stepIndex];
            PdfImageOutputRow outputRow = step.GetOutputRow(inputRowIndex);

            if (outputRow.HasNextRow)
            {
                _pendingStep = stepIndex;
                _pendingRowIndex = inputRowIndex;
            }

            if (stepIndex == _steps.Length - 1 && !outputRow.IsEmpty)
            {
                step.Process(input, _target.GetRow(outputRow.RowIndex), inputRowIndex);
                WrittenRowCount++;

                return;
            }

            Span<byte> output = _buffer.AsSpan(0, _outputLengths[stepIndex]);
            step.Process(input, output, inputRowIndex);

            if (!outputRow.IsEmpty)
            {
                input = output;
                inputRowIndex = outputRow.RowIndex;
                stepIndex++;
                continue;
            }

            if (_pendingStep == NoPendingStep)
            {
                return;
            }

            input = ReadOnlySpan<byte>.Empty;
            inputRowIndex = _pendingRowIndex;
            stepIndex = _pendingStep;
            _pendingStep = NoPendingStep;
        }
    }

    /// <summary>
    /// Sets the length of the row every step writes for input rows of <paramref name="inputRowLength"/> bytes.
    /// </summary>
    private void SetOutputLengths(int inputRowLength)
    {
        int length = inputRowLength;

        for (int stepIndex = 0; stepIndex < _steps.Length; stepIndex++)
        {
            length = _steps[stepIndex].GetOutputLength(length);
            _outputLengths[stepIndex] = length;

            if (length > _buffer.Length)
            {
                throw new InvalidOperationException($"Step {stepIndex} writes rows of {length} bytes, the buffer holds {_buffer.Length}.");
            }
        }

        if (length != _target.RowBytes)
        {
            throw new InvalidOperationException($"The last step writes rows of {length} bytes, the target has rows of {_target.RowBytes}.");
        }

        _hasOutputLengths = true;
    }
}
