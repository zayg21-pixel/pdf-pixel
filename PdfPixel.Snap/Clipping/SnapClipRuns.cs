using PdfPixel.Snap.Rasterization;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Clipping;

/// <summary>
/// Merges coverage rows with clip rows run by run: a pixel full in both stays full, any other takes the product of its
/// values.
/// </summary>
internal static class SnapClipRuns
{
    private static readonly float[] One = [1f];

    /// <summary>
    /// Returns the pixels inside both <paramref name="coverage"/> and <paramref name="clip"/>.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public static SnapCoverageRow Intersect(in SnapCoverageRow coverage, in SnapClipRow clip, SnapClipBuffer buffer)
    {
        ReadOnlySpan<SnapCoverageRun> coverageRuns = coverage.Runs;
        ReadOnlySpan<SnapClipRun> clipRuns = clip.Runs;
        Span<SnapCoverageRun> runs = buffer.Runs;
        Span<float> values = buffer.Values;
        int runCount = 0;
        int coverageIndex = 0;
        int clipIndex = 0;

        while (coverageIndex < coverageRuns.Length && clipIndex < clipRuns.Length)
        {
            SnapCoverageRun coverageRun = coverageRuns[coverageIndex];
            SnapClipRun clipRun = clipRuns[clipIndex];
            int coverageRight = coverageRun.Left + coverageRun.Count;
            int clipRight = Math.Min(clipRun.Left + clipRun.Count, clip.Right);
            int left = Math.Max(Math.Max(coverageRun.Left, clipRun.Left), clip.Left);
            int right = Math.Min(coverageRight, clipRight);

            if (left < right)
            {
                if (coverageRun.IsFull && clipRun.IsFull)
                {
                    runs[runCount] = new SnapCoverageRun(left, right - left, isFull: true);
                }
                else
                {
                    ref float coverageValue = ref MemoryMarshal.GetReference(GetCoverageValues(coverage, coverageRun, left));
                    ref float clipValue = ref MemoryMarshal.GetReference(GetClipValues(clip, clipRun, left));
                    ref float value = ref Unsafe.Add(ref MemoryMarshal.GetReference(values), left - clip.Left);
                    int coverageStep = (coverageRun.IsFull) ? 0 : 1;
                    int clipStep = (clipRun.IsFull) ? 0 : 1;

                    for (int x = left; x < right; x++)
                    {
                        value = coverageValue * clipValue;
                        value = ref Unsafe.Add(ref value, 1);
                        coverageValue = ref Unsafe.Add(ref coverageValue, coverageStep);
                        clipValue = ref Unsafe.Add(ref clipValue, clipStep);
                    }

                    runs[runCount] = new SnapCoverageRun(left, right - left, isFull: false);
                }

                runCount++;
            }

            if (coverageRight < clipRight)
            {
                coverageIndex++;
            }
            else
            {
                clipIndex++;
            }
        }

        return new SnapCoverageRow(clip.Y, clip.Left, runs.Slice(0, runCount), values);
    }

    private static ReadOnlySpan<float> GetCoverageValues(in SnapCoverageRow coverage, in SnapCoverageRun run, int x)
    {
        if (run.IsFull)
        {
            return One;
        }
        else
        {
            return coverage.Values.Slice(x - coverage.Left);
        }
    }

    private static ReadOnlySpan<float> GetClipValues(in SnapClipRow clip, in SnapClipRun run, int x)
    {
        if (run.IsFull)
        {
            return One;
        }
        else
        {
            return clip.Values.Slice(run.ValueIndex + (x - run.Left));
        }
    }
}
