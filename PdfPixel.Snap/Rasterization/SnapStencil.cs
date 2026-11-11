using System;

namespace PdfPixel.Snap.Rasterization;

/// <summary>
/// The coverage of a shape rasterized once at one size and subpixel position, placed wherever the shape is drawn again.
/// </summary>
internal sealed class SnapStencil
{
    private readonly float[] _values;
    private readonly SnapCoverageRun[] _runs;
    private readonly int[] _rowRunStarts;

    /// <summary>
    /// Initializes a stencil; <paramref name="rowRunStarts"/> holds each row's first run in <paramref name="runs"/>,
    /// followed by the run count.
    /// </summary>
    public SnapStencil(int left, int top, int width, int height, float[] values, SnapCoverageRun[] runs, int[] rowRunStarts)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        _values = values;
        _runs = runs;
        _rowRunStarts = rowRunStarts;
    }

    /// <summary>
    /// Gets the stencil's first column, counted from the whole pixel the shape's origin is placed in.
    /// </summary>
    public int Left { get; }

    /// <summary>
    /// Gets the stencil's first row, counted from the whole pixel the shape's origin is placed in.
    /// </summary>
    public int Top { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>
    /// Returns the covered runs of <paramref name="row"/>, their columns counted from the stencil's first column.
    /// </summary>
    public ReadOnlySpan<SnapCoverageRun> GetRowRuns(int row) => _runs.AsSpan(_rowRunStarts[row], _rowRunStarts[row + 1] - _rowRunStarts[row]);

    /// <summary>
    /// Returns the coverage of every pixel of <paramref name="row"/>, read for partly covered runs only.
    /// </summary>
    public ReadOnlySpan<float> GetRowValues(int row) => _values.AsSpan(row * Width, Width);
}
