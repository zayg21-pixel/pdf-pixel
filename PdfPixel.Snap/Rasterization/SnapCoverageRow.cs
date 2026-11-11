using PdfPixel.Snap.Painting;
using System;

namespace PdfPixel.Snap.Rasterization;

/// <summary>
/// One row of an outline's coverage, valid until the rasterizer produces the next row.
/// </summary>
internal readonly ref struct SnapCoverageRow
{
    public SnapCoverageRow(int y, int left, scoped in ReadOnlySpan<SnapCoverageRun> runs, scoped in ReadOnlySpan<float> values)
    {
        Y = y;
        Left = left;
        Runs = runs;
        Values = values;
    }

    public int Y { get; }

    /// <summary>
    /// Gets the pixel column <see cref="Values"/> starts at.
    /// </summary>
    public int Left { get; }

    public ReadOnlySpan<SnapCoverageRun> Runs { get; }

    /// <summary>
    /// Gets the coverage of each pixel from column <see cref="Left"/>, read for partly covered runs only.
    /// </summary>
    public ReadOnlySpan<float> Values { get; }

    /// <summary>
    /// Returns the coverage of the pixels of <paramref name="run"/>: 1 for every pixel of a fully covered run.
    /// </summary>
    public SnapRowValues GetCoverage(in SnapCoverageRun run)
    {
        if (run.IsFull)
        {
            return SnapRowValues.One;
        }
        else
        {
            return new SnapRowValues(Values.Slice(run.Left - Left, run.Count), false);
        }
    }
}
