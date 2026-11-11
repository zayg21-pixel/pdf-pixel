using PdfPixel.Imaging.Processing.Pipeline;
using System;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// The sample rows of a window of an image source in the order its pipeline reads them, from the bottom of the grid
/// up when the image is drawn upside down.
/// </summary>
internal sealed class SnapImageRowWindow : IPdfImageRowSource
{
    private readonly SnapImageReader _reader;
    private readonly int _rowStart;
    private readonly int _gridHeight;
    private readonly bool _readsBottomUp;

    public SnapImageRowWindow(SnapImageReader reader, int rowStart, int rowCount, int gridHeight, bool readsBottomUp)
    {
        _reader = reader;
        _rowStart = rowStart;
        RowCount = rowCount;
        _gridHeight = gridHeight;
        _readsBottomUp = readsBottomUp;
    }

    /// <inheritdoc />
    public int RowCount { get; }

    /// <inheritdoc />
    public ReadOnlySpan<byte> ReadRow(int rowIndex)
    {
        int orientedRow = _rowStart + rowIndex;
        int sampleRow = _readsBottomUp ? _gridHeight - 1 - orientedRow : orientedRow;

        return _reader.ReadRow(sampleRow);
    }
}
