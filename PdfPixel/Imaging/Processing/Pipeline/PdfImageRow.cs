using System;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// A row produced by an <see cref="IPdfImageRowProcessingStep"/>.
/// </summary>
public readonly ref struct PdfImageRow
{
    /// <summary>
    /// Initializes a new <see cref="PdfImageRow"/> over <paramref name="span"/>.
    /// </summary>
    /// <param name="span">Bytes of the row.</param>
    /// <param name="rowIndex">Index of the row in the output of the step.</param>
    /// <param name="hasNextRow">Whether the step produces another row from the same input.</param>
    public PdfImageRow(scoped in ReadOnlySpan<byte> span, int rowIndex, bool hasNextRow)
    {
        Span = span;
        RowIndex = rowIndex;
        HasNextRow = hasNextRow;
    }

    /// <summary>
    /// Gets a row holding no bytes.
    /// </summary>
    public static PdfImageRow Empty => default;

    /// <summary>
    /// Gets the bytes of the row.
    /// </summary>
    public ReadOnlySpan<byte> Span { get; }

    /// <summary>
    /// Gets the index of the row in the output of the step.
    /// </summary>
    public int RowIndex { get; }

    /// <summary>
    /// Gets whether the step produces another row from the same input.
    /// </summary>
    public bool HasNextRow { get; }

    /// <summary>
    /// Gets whether the row holds no bytes.
    /// </summary>
    public bool IsEmpty => Span.IsEmpty;
}
