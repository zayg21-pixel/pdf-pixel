using System;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Runs source rows through a chain of <see cref="IPdfImageRowProcessingStep"/> and writes the rows
/// they produce into a <see cref="PdfImageSamples"/>.
/// </summary>
public sealed class PdfImageRowProcessingPipeline
{
    private readonly IPdfImageRowProcessingStep[] _steps;
    private readonly PdfImageSamples _samples;

    private int _writtenRowCount;

    /// <summary>
    /// Initializes a pipeline running <paramref name="steps"/> in order and writing into <paramref name="samples"/>.
    /// </summary>
    /// <param name="steps">Steps each row runs through, in order.</param>
    /// <param name="samples">Image the produced rows are written into.</param>
    public PdfImageRowProcessingPipeline(IPdfImageRowProcessingStep[] steps, PdfImageSamples samples)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _samples = samples ?? throw new ArgumentNullException(nameof(samples));
    }

    /// <summary>
    /// Gets whether every row of the image has been written.
    /// </summary>
    public bool IsComplete => _writtenRowCount >= _samples.Height;

    /// <summary>
    /// Gets the image once every row has been written.
    /// </summary>
    /// <exception cref="InvalidOperationException">Not every row has been written.</exception>
    public PdfImageSamples Image
    {
        get
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException($"Only {_writtenRowCount} of {_samples.Height} rows have been written.");
            }

            return _samples;
        }
    }

    /// <summary>
    /// Runs the source row at <paramref name="rowIndex"/> through the steps.
    /// </summary>
    /// <param name="row">Bytes of the source row.</param>
    /// <param name="rowIndex">Index of the source row.</param>
    public void ProcessRow(in ReadOnlySpan<byte> row, int rowIndex) => ProcessFrom(0, row, rowIndex);

    private void ProcessFrom(int stepIndex, in ReadOnlySpan<byte> input, int inputRowIndex)
    {
        if (stepIndex == _steps.Length)
        {
            WriteRow(input, inputRowIndex);
        }
        else
        {
            PdfImageRow row;

            do
            {
                row = _steps[stepIndex].Process(input, inputRowIndex);

                if (row.IsEmpty)
                {
                    return;
                }

                ProcessFrom(stepIndex + 1, row.Span, row.RowIndex);
            }
            while (row.HasNextRow);
        }
    }

    private void WriteRow(in ReadOnlySpan<byte> row, int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _samples.Height)
        {
            throw new InvalidOperationException($"Row {rowIndex} is outside the {_samples.Height} rows of the image.");
        }

        if (row.Length < _samples.RowBytes)
        {
            throw new InvalidOperationException($"Row {rowIndex} holds {row.Length} bytes, the image needs {_samples.RowBytes}.");
        }

        row.Slice(0, _samples.RowBytes).CopyTo(_samples.GetRow(rowIndex));
        _writtenRowCount++;
    }
}
