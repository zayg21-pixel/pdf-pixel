using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Snap.Rasterization;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap.Clipping;

/// <summary>
/// Builds a clip by intersecting an outline's coverage rows with the current clip: rows the outline misses are empty.
/// </summary>
internal struct SnapClipBuilder : ISnapCoverageSink
{
    private readonly SnapClip _current;
    private readonly SnapClipBuffer _buffer;
    private readonly PdfIntegerRectangle _limits;
    private readonly PdfValueList<SnapClipRun> _runs;
    private readonly PdfValueList<float> _values;
    private readonly int[] _rowStarts;
    private int _nextY;
    private int _left;
    private int _top;
    private int _right;
    private int _bottom;

    public SnapClipBuilder(SnapClip current, SnapClipBuffer buffer)
    {
        _current = current;
        _buffer = buffer;
        _limits = current.Bounds;
        _runs = new PdfValueList<SnapClipRun>();
        _values = new PdfValueList<float>();
        _rowStarts = new int[_limits.Height + 1];
        _nextY = _limits.Top;
        _left = int.MaxValue;
        _top = int.MaxValue;
        _right = int.MinValue;
        _bottom = int.MinValue;
        buffer.Prepare(_limits.Width);
    }

    /// <inheritdoc />
    public void AddRow(in SnapCoverageRow row)
    {
        AddRowsUntil(row.Y);

        AppendRow(SnapClipRuns.Intersect(row, _current.GetRow(row.Y), _buffer));

        _nextY = row.Y + 1;
    }

    /// <summary>
    /// Completes the rows the outline did not reach and returns the clip.
    /// </summary>
    public SnapClip ToClip()
    {
        AddRowsUntil(_limits.Bottom);
        _rowStarts[_limits.Height] = _runs.Count;

        if (_runs.Count == 0)
        {
            return SnapClip.CreateRectangle(PdfIntegerRectangle.Empty);
        }
        else
        {
            PdfIntegerRectangle bounds = new(_left, _top, _right, _bottom);

            return SnapClip.FromRows(bounds, _runs.Items.ToArray(), _rowStarts, _values.Items.ToArray(), _limits.Top);
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void AddRowsUntil(int y)
    {
        for (int rowY = _nextY; rowY < y; rowY++)
        {
            _rowStarts[rowY - _limits.Top] = _runs.Count;
        }

        _nextY = Math.Max(_nextY, y);
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void AppendRow(in SnapCoverageRow row)
    {
        _rowStarts[row.Y - _limits.Top] = _runs.Count;

        foreach (SnapCoverageRun run in row.Runs)
        {
            if (run.IsFull)
            {
                _runs.Add(new SnapClipRun(run.Left, run.Count, isFull: true, valueIndex: 0));
            }
            else
            {
                _runs.Add(new SnapClipRun(run.Left, run.Count, isFull: false, _values.Count));

                foreach (float value in row.Values.Slice(run.Left - row.Left, run.Count))
                {
                    _values.Add(value);
                }
            }

            _left = Math.Min(_left, run.Left);
            _right = Math.Max(_right, run.Left + run.Count);
        }

        if (row.Runs.Length > 0)
        {
            _top = Math.Min(_top, row.Y);
            _bottom = Math.Max(_bottom, row.Y + 1);
        }
    }
}
