namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// The row an <see cref="IPdfImageRowProcessingStep"/> writes for an input row.
/// </summary>
public readonly struct PdfImageOutputRow
{
    private readonly bool _isWritten;

    /// <summary>
    /// Initializes a new <see cref="PdfImageOutputRow"/>.
    /// </summary>
    /// <param name="rowIndex">Index of the row in the output of the step.</param>
    /// <param name="hasNextRow">Whether the step writes another row from the same input.</param>
    public PdfImageOutputRow(int rowIndex, bool hasNextRow)
    {
        _isWritten = true;
        RowIndex = rowIndex;
        HasNextRow = hasNextRow;
    }

    /// <summary>
    /// Gets the output row of an input row the step writes nothing for.
    /// </summary>
    public static PdfImageOutputRow Empty => default;

    /// <summary>
    /// Gets the index of the row in the output of the step.
    /// </summary>
    public int RowIndex { get; }

    /// <summary>
    /// Gets whether the step writes another row from the same input.
    /// </summary>
    public bool HasNextRow { get; }

    /// <summary>
    /// Gets whether the step writes no row.
    /// </summary>
    public bool IsEmpty => !_isWritten;
}
