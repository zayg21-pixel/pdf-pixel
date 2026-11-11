using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using System;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// The rows of a rectangle of a layer result, the result's own pixel format, as the input rows of a pipeline.
/// </summary>
internal sealed class SnapResultRows : IPdfImageRowSource
{
    private readonly PdfImageSamples _result;
    private readonly int _top;
    private readonly int _byteOffset;
    private readonly int _rowLength;

    /// <summary>
    /// Initializes the rows of <paramref name="rectangle"/>, given in the pixels of <paramref name="result"/>.
    /// </summary>
    public SnapResultRows(PdfImageSamples result, in PdfIntegerRectangle rectangle)
    {
        int pixelBytes = result.RowBytes / result.Width;

        _result = result;
        _top = rectangle.Top;
        _byteOffset = rectangle.Left * pixelBytes;
        _rowLength = rectangle.Width * pixelBytes;
        RowCount = rectangle.Height;
    }

    /// <inheritdoc />
    public int RowCount { get; }

    /// <summary>
    /// Gets the number of bytes of a row.
    /// </summary>
    public int RowLength => _rowLength;

    /// <inheritdoc />
    public ReadOnlySpan<byte> ReadRow(int rowIndex) => _result.GetRow(_top + rowIndex).Slice(_byteOffset, _rowLength);
}
