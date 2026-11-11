using System;

namespace PdfPixel.Snap.Clipping;

/// <summary>
/// One row of a <see cref="SnapClip"/>; only the columns from <see cref="Left"/> to <see cref="Right"/> belong to
/// the clip, whatever its runs span.
/// </summary>
internal readonly ref struct SnapClipRow
{
    public SnapClipRow(int y, int left, int right, scoped in ReadOnlySpan<SnapClipRun> runs, scoped in ReadOnlySpan<float> values)
    {
        Y = y;
        Left = left;
        Right = right;
        Runs = runs;
        Values = values;
    }

    public int Y { get; }

    public int Left { get; }

    public int Right { get; }

    public ReadOnlySpan<SnapClipRun> Runs { get; }

    /// <summary>
    /// Gets the clip's values, which the partial runs index.
    /// </summary>
    public ReadOnlySpan<float> Values { get; }
}
