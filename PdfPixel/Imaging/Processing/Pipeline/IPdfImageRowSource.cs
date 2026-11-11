using System;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// The input rows of an image row processing pipeline, in input order.
/// </summary>
public interface IPdfImageRowSource
{
    /// <summary>
    /// Gets the number of input rows.
    /// </summary>
    int RowCount { get; }

    /// <summary>
    /// Returns the bytes of input row <paramref name="rowIndex"/>, valid until the next read.
    /// </summary>
    /// <param name="rowIndex">Index of the input row.</param>
    ReadOnlySpan<byte> ReadRow(int rowIndex);
}
