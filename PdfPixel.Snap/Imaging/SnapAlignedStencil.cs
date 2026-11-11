using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Snap.Rasterization;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// An axis-aligned stencil mask read straight from its bits: each device pixel's coverage is the share of the samples
/// in its footprint that paint.
/// </summary>
internal sealed class SnapAlignedStencil : SnapImageStencil
{
    private readonly SnapImageReader _reader;
    private readonly PdfImageRowAxisMapping _vertical;
    private readonly PdfIntegerRectangle _pixels;
    private readonly bool _readsBottomUp;
    private readonly bool _paintsOnZero;
    private readonly int[] _columnStarts;
    private readonly int[] _columnEnds;
    private readonly int[] _counts;
    private readonly float[] _values;
    private readonly SnapCoverageRun[] _runs;
    private int _runCount;

    /// <summary>
    /// Initializes the stencil; <paramref name="columnStarts"/> and <paramref name="columnEnds"/> give each device
    /// column's sample footprint.
    /// </summary>
    public SnapAlignedStencil(
        SnapImageReader reader,
        in PdfImageRowAxisMapping vertical,
        in PdfIntegerRectangle pixels,
        bool readsBottomUp,
        bool paintsOnZero,
        int[] columnStarts,
        int[] columnEnds)
    {
        _reader = reader;
        _vertical = vertical;
        _pixels = pixels;
        _readsBottomUp = readsBottomUp;
        _paintsOnZero = paintsOnZero;
        _columnStarts = columnStarts;
        _columnEnds = columnEnds;
        _counts = new int[pixels.Width];
        _values = new float[pixels.Width];
        _runs = new SnapCoverageRun[pixels.Width];
    }

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public override void Rasterize<TSink>(ref TSink sink)
    {
        PdfImageRowSampleRange countedRows = new(0, 0);

        for (int row = 0; row < _pixels.Height; row++)
        {
            PdfImageRowSampleRange sourceRows = _vertical.GetFootprint(row);

            if (sourceRows.Start != countedRows.Start || sourceRows.End != countedRows.End)
            {
                CountRows(sourceRows);
                BuildRuns(sourceRows.Count);
                countedRows = sourceRows;
            }

            if (_runCount > 0)
            {
                SnapCoverageRow coverageRow = new(_pixels.Top + row, _pixels.Left, _runs.AsSpan(0, _runCount), _values);
                sink.AddRow(coverageRow);
            }
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void CountRows(in PdfImageRowSampleRange sourceRows)
    {
        int width = _pixels.Width;
        ref int count = ref _counts[0];
        ref int columnStart = ref _columnStarts[0];
        ref int columnEnd = ref _columnEnds[0];

        _counts.AsSpan().Clear();

        for (int orientedRow = sourceRows.Start; orientedRow < sourceRows.End; orientedRow++)
        {
            int sampleRow = _readsBottomUp ? _vertical.SourceCount - 1 - orientedRow : orientedRow;
            ref byte bits = ref MemoryMarshal.GetReference(_reader.ReadRow(sampleRow));

            for (int column = 0; column < width; column++)
            {
                Unsafe.Add(ref count, column) += PdfPackedSamples.CountSetBits(ref bits, Unsafe.Add(ref columnStart, column), Unsafe.Add(ref columnEnd, column));
            }
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void BuildRuns(int rowCount)
    {
        int width = _pixels.Width;
        int left = _pixels.Left;
        ref int count = ref _counts[0];
        ref int columnStart = ref _columnStarts[0];
        ref int columnEnd = ref _columnEnds[0];
        ref float value = ref _values[0];
        var runKind = CoverageKind.Empty;
        int runStart = 0;

        _runCount = 0;

        for (int column = 0; column < width; column++)
        {
            int area = rowCount * (Unsafe.Add(ref columnEnd, column) - Unsafe.Add(ref columnStart, column));
            int setCount = Unsafe.Add(ref count, column);
            int painted = _paintsOnZero ? area - setCount : setCount;
            CoverageKind kind;

            if (painted <= 0)
            {
                kind = CoverageKind.Empty;
            }
            else if (painted >= area)
            {
                kind = CoverageKind.Full;
            }
            else
            {
                kind = CoverageKind.Partial;
                Unsafe.Add(ref value, column) = painted / (float)area;
            }

            if (kind != runKind)
            {
                AddRun(runKind, left + runStart, column - runStart);
                runKind = kind;
                runStart = column;
            }
        }

        AddRun(runKind, left + runStart, width - runStart);
    }

    private void AddRun(CoverageKind kind, int left, int count)
    {
        if (kind != CoverageKind.Empty && count > 0)
        {
            _runs[_runCount] = new SnapCoverageRun(left, count, isFull: kind == CoverageKind.Full);
            _runCount++;
        }
    }

    private enum CoverageKind
    {
        Empty,
        Full,
        Partial
    }

    /// <inheritdoc />
    public override void Dispose() => _reader.Dispose();
}
