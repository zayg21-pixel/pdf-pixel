using PdfPixel.Imaging.Processing.Pipeline;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// The output rows of an image source: its sample rows run through its pipeline as far as asked, the last ones kept in a
/// ring of a fixed number of rows.
/// </summary>
internal sealed class SnapImageRowSource : IPdfImageRowTarget, IDisposable
{
    private readonly SnapImageReader _reader;
    private readonly int _channelCount;
    private readonly int _outputWidth;
    private readonly int _keptRowCount;
    private readonly SnapMemoryBlock _rows;
    private readonly SnapMemoryBlock _zeroRow;
    private readonly SnapMemoryBlock _pipelineBuffer;
    private readonly PdfImageRowProcessingPipeline _pipeline;
    private int _firstKeptRow;

    /// <summary>
    /// Initializes the output rows of the <paramref name="sourceRowCount"/> sample rows from
    /// <paramref name="sourceRowStart"/> read by <paramref name="reader"/> through <paramref name="steps"/>, keeping at
    /// most <paramref name="keptRowCount"/> of them; its buffers are rented from <paramref name="memoryPool"/> until
    /// <see cref="Dispose"/>, which also disposes the reader.
    /// </summary>
    public SnapImageRowSource(
        SnapImageReader reader,
        int sourceRowStart,
        int sourceRowCount,
        int gridHeight,
        bool readsBottomUp,
        IPdfImageRowProcessingStep[] steps,
        int channelCount,
        int outputWidth,
        int keptRowCount,
        PdfSnapMemoryPool memoryPool)
    {
        _reader = reader;
        _channelCount = channelCount;
        _outputWidth = outputWidth;
        _keptRowCount = keptRowCount;
        _rows = memoryPool.Rent(keptRowCount * RowBytes);
        _zeroRow = memoryPool.Rent(RowBytes);
        _zeroRow.Array.AsSpan(0, RowBytes).Clear();
        _pipelineBuffer = memoryPool.Rent(PdfImageRowProcessingPipeline.GetBufferLength(steps, reader.RowLength));

        SnapImageRowWindow window = new(reader, sourceRowStart, sourceRowCount, gridHeight, readsBottomUp);
        _pipeline = new PdfImageRowProcessingPipeline(window, steps, this, _pipelineBuffer.Array);
    }

    public int ChannelCount => _channelCount;

    /// <inheritdoc />
    public int RowBytes => _channelCount * _outputWidth * sizeof(float);

    public ReadOnlySpan<float> ReadOutputRow(int rowIndex)
    {
        PrepareOutputRows(rowIndex, rowIndex);

        return GetOutputRow(rowIndex);
    }

    /// <summary>
    /// Writes output rows until <paramref name="lastRow"/>, dropping rows before <paramref name="firstRow"/>.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void PrepareOutputRows(int firstRow, int lastRow)
    {
        _firstKeptRow = Math.Max(_firstKeptRow, firstRow);
        _pipeline.ProcessUntilRow(lastRow);
    }

    /// <summary>
    /// Returns a kept output row, valid until rows are prepared again; zeros when the pipeline ended before writing it.
    /// </summary>
    public ReadOnlySpan<float> GetOutputRow(int rowIndex)
    {
        if (rowIndex >= _pipeline.WrittenRowCount || rowIndex < _firstKeptRow)
        {
            return MemoryMarshal.Cast<byte, float>(_zeroRow.Array.AsSpan(0, RowBytes));
        }

        return MemoryMarshal.Cast<byte, float>(GetSlot(rowIndex));
    }

    /// <inheritdoc />
    public Span<byte> GetRow(int rowIndex)
    {
        if (rowIndex - _firstKeptRow >= _keptRowCount)
        {
            throw new InvalidOperationException($"Output row {rowIndex} does not fit the {_keptRowCount} rows kept from row {_firstKeptRow}.");
        }

        return GetSlot(rowIndex);
    }

    private Span<byte> GetSlot(int rowIndex) => _rows.Array.AsSpan((rowIndex % _keptRowCount) * RowBytes, RowBytes);

    /// <inheritdoc />
    public void Dispose()
    {
        _reader.Dispose();
        _pipelineBuffer.Dispose();
        _zeroRow.Dispose();
        _rows.Dispose();
    }
}
