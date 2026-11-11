using PdfPixel.Snap.Rasterization;

namespace PdfPixel.Snap.Clipping;

/// <summary>
/// Passes coverage rows on to <see cref="Sink"/> intersected with a clip, dropping rows left empty.
/// </summary>
internal struct SnapClippedCoverageSink<TSink> : ISnapCoverageSink
    where TSink : ISnapCoverageSink
{
    private readonly SnapClip _clip;
    private readonly SnapClipBuffer _buffer;
    private TSink _sink;

    public SnapClippedCoverageSink(SnapClip clip, SnapClipBuffer buffer, TSink sink)
    {
        _clip = clip;
        _buffer = buffer;
        _sink = sink;
        buffer.Prepare(clip.Bounds.Width);
    }

    /// <summary>
    /// Gets the wrapped sink with the rows it received.
    /// </summary>
    public readonly TSink Sink => _sink;

    /// <inheritdoc />
    public void AddRow(in SnapCoverageRow row)
    {
        SnapCoverageRow clippedRow = SnapClipRuns.Intersect(row, _clip.GetRow(row.Y), _buffer);

        if (clippedRow.Runs.Length > 0)
        {
            _sink.AddRow(clippedRow);
        }
    }
}
