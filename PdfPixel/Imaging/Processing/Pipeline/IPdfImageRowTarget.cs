using System;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// The rows a <see cref="PdfImageRowProcessingPipeline"/> writes its output into.
/// </summary>
public interface IPdfImageRowTarget
{
    /// <summary>
    /// Gets the number of bytes of a row.
    /// </summary>
    int RowBytes { get; }

    /// <summary>
    /// Returns room for the output row at <paramref name="rowIndex"/>.
    /// </summary>
    Span<byte> GetRow(int rowIndex);
}
