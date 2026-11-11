using PdfPixel.Snap.Rasterization;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// Passes an image footprint's coverage rows on multiplied by a stencil mask's coverage, split again into runs within
/// half an 8-bit level of full or empty.
/// </summary>
internal struct SnapStencilCoverageSink<TSink> : ISnapCoverageSink
    where TSink : ISnapCoverageSink
{
    private const float EmptyCoverage = 0.5f / 255;
    private const float FullCoverage = 1 - (0.5f / 255);

    private readonly SnapImagePlacement _stencil;
    private readonly float[] _values;
    private readonly SnapCoverageRun[] _runs;
    private TSink _sink;

    public SnapStencilCoverageSink(SnapImagePlacement stencil, int width, TSink sink)
    {
        _stencil = stencil;
        _values = new float[width];
        _runs = new SnapCoverageRun[width];
        _sink = sink;
    }

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void AddRow(in SnapCoverageRow row)
    {
        ReadOnlySpan<SnapCoverageRun> footprintRuns = row.Runs;
        SnapCoverageRun lastRun = footprintRuns[footprintRuns.Length - 1];
        int left = footprintRuns[0].Left;
        Span<float> values = _values.AsSpan(0, lastRun.Left + lastRun.Count - left);
        int written = 0;

        _stencil.ReadRow(row.Y, left, values);

        foreach (SnapCoverageRun run in footprintRuns)
        {
            int start = run.Left - left;

            values.Slice(written, start - written).Clear();

            if (!run.IsFull)
            {
                MultiplyCoverage(values.Slice(start, run.Count), row.Values.Slice(run.Left - row.Left, run.Count));
            }

            written = start + run.Count;
        }

        int runCount = BuildRuns(values, left);

        if (runCount > 0)
        {
            SnapCoverageRow narrowedRow = new(row.Y, left, _runs.AsSpan(0, runCount), values);
            _sink.AddRow(narrowedRow);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void MultiplyCoverage(in Span<float> values, in ReadOnlySpan<float> coverage)
    {
        ref float value = ref MemoryMarshal.GetReference(values);
        ref float factor = ref MemoryMarshal.GetReference(coverage);

        for (int index = 0; index < values.Length; index++)
        {
            Unsafe.Add(ref value, index) *= Unsafe.Add(ref factor, index);
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private readonly int BuildRuns(in ReadOnlySpan<float> values, int left)
    {
        ref float value = ref MemoryMarshal.GetReference(values);
        int runCount = 0;
        int runStart = 0;
        var runIsCovered = false;
        var runIsFull = false;

        for (int index = 0; index < values.Length; index++)
        {
            float coverage = Unsafe.Add(ref value, index);
            bool isCovered = coverage > EmptyCoverage;
            bool isFull = coverage >= FullCoverage;

            if (isCovered != runIsCovered || isFull != runIsFull)
            {
                runCount = AddRun(runCount, runIsCovered, runIsFull, left + runStart, index - runStart);
                runIsCovered = isCovered;
                runIsFull = isFull;
                runStart = index;
            }
        }

        return AddRun(runCount, runIsCovered, runIsFull, left + runStart, values.Length - runStart);
    }

    private readonly int AddRun(int runCount, bool isCovered, bool isFull, int left, int count)
    {
        if (isCovered && count > 0)
        {
            _runs[runCount] = new SnapCoverageRun(left, count, isFull);
            runCount++;
        }

        return runCount;
    }
}
