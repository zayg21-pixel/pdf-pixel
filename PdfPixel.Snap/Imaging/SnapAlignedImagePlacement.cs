using PdfPixel.Geometry;
using System;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// An axis-aligned image placement: output row n of the source is device row n of the visible area.
/// </summary>
internal sealed class SnapAlignedImagePlacement : SnapImagePlacement
{
    private readonly SnapImageRowSource _source;
    private readonly PdfIntegerRectangle _visibleArea;

    public SnapAlignedImagePlacement(SnapImageRowSource source, in PdfIntegerRectangle visibleArea)
        : base(source.ChannelCount)
    {
        _source = source;
        _visibleArea = visibleArea;
    }

    /// <inheritdoc />
    public override void ReadRow(int y, int left, in Span<float> destination)
    {
        ReadOnlySpan<float> row = _source.ReadOutputRow(y - _visibleArea.Top);

        row.Slice((left - _visibleArea.Left) * ChannelCount, destination.Length).CopyTo(destination);
    }

    /// <inheritdoc />
    public override void Dispose() => _source.Dispose();
}
