using PdfPixel.Geometry;
using PdfPixel.Objects;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap.Rasterization;

/// <summary>
/// Collects the coverage rows of a shape into a <see cref="SnapStencil"/>.
/// </summary>
internal struct SnapStencilSink : ISnapCoverageSink
{
    private readonly PdfIntegerRectangle _bounds;
    private readonly float[] _values;
    private readonly PdfValueList<SnapCoverageRun> _runs;
    private readonly int[] _rowRunStarts;
    private int _nextRow;

    /// <summary>
    /// Initializes a sink for the pixels of <paramref name="bounds"/>, relative to the whole pixel the shape's origin
    /// is placed in.
    /// </summary>
    public SnapStencilSink(in PdfIntegerRectangle bounds)
    {
        _bounds = bounds;
        _values = new float[bounds.Width * bounds.Height];
        _runs = new PdfValueList<SnapCoverageRun>();
        _rowRunStarts = new int[bounds.Height + 1];
        _nextRow = 0;
    }

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void AddRow(in SnapCoverageRow row)
    {
        int stencilRow = row.Y - _bounds.Top;
        StartRowsUpTo(stencilRow);

        foreach (SnapCoverageRun run in row.Runs)
        {
            int column = run.Left - _bounds.Left;
            _runs.Add(new SnapCoverageRun(column, run.Count, run.IsFull));

            if (!run.IsFull)
            {
                row.Values.Slice(run.Left - row.Left, run.Count).CopyTo(_values.AsSpan((stencilRow * _bounds.Width) + column, run.Count));
            }
        }
    }

    public SnapStencil ToStencil()
    {
        StartRowsUpTo(_bounds.Height);

        return new SnapStencil(_bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height, _values, _runs.Items.ToArray(), _rowRunStarts);
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void StartRowsUpTo(int row)
    {
        while (_nextRow <= row)
        {
            _rowRunStarts[_nextRow] = _runs.Count;
            _nextRow++;
        }
    }
}
