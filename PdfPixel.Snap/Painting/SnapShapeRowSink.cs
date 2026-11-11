using PdfPixel.Snap.Rasterization;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap.Painting;

/// <summary>
/// Turns each coverage row into per-pixel shapes from its first covered column to its last, 0 between runs, and hands
/// them to a painter.
/// </summary>
internal readonly struct SnapShapeRowSink<TPainter> : ISnapCoverageSink
    where TPainter : ISnapRowPainter
{
    private readonly TPainter _painter;
    private readonly SnapScratchBuffer _scratch;

    public SnapShapeRowSink(TPainter painter, SnapScratchBuffer scratch)
    {
        _painter = painter;
        _scratch = scratch;
    }

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void AddRow(in SnapCoverageRow row)
    {
        ReadOnlySpan<SnapCoverageRun> runs = row.Runs;
        SnapCoverageRun lastRun = runs[runs.Length - 1];
        int left = runs[0].Left;
        Span<float> shapes = _scratch.GetRowShapes(lastRun.Left + lastRun.Count - left);
        int written = 0;

        foreach (SnapCoverageRun run in runs)
        {
            int start = run.Left - left;
            Span<float> runShapes = shapes.Slice(start, run.Count);

            shapes.Slice(written, start - written).Clear();

            if (run.IsFull)
            {
                runShapes.Fill(1f);
            }
            else
            {
                row.Values.Slice(run.Left - row.Left, run.Count).CopyTo(runShapes);
            }

            written = start + run.Count;
        }

        _painter.PaintRow(row.Y, left, shapes);
    }
}
