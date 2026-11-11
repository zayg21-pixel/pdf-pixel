using PdfPixel.Geometry;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap.Imaging;

// TODO: [MEDIUM] A quarter turn makes the band span every resized row under the visible width. A reader that reads
// sample columns as rows would turn it into the aligned case.
/// <summary>
/// A turned or skewed image placement: each device pixel takes the sample of the resized source its center maps back
/// to, from the band of resized rows its device row crosses.
/// </summary>
internal sealed class SnapTransformedImagePlacement : SnapImagePlacement
{
    private readonly SnapImageRowSource _source;
    private readonly PdfMatrix _deviceToResized;
    private readonly PdfIntegerRectangle _visibleArea;
    private readonly int _resizedWidth;
    private readonly int _resizedHeight;

    public SnapTransformedImagePlacement(
        SnapImageRowSource source,
        in PdfMatrix deviceToResized,
        in PdfIntegerRectangle visibleArea,
        int resizedWidth,
        int resizedHeight)
        : base(source.ChannelCount)
    {
        _source = source;
        _deviceToResized = deviceToResized;
        _visibleArea = visibleArea;
        _resizedWidth = resizedWidth;
        _resizedHeight = resizedHeight;
    }

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public override void ReadRow(int y, int left, in Span<float> destination)
    {
        int count = destination.Length / ChannelCount;
        float centerY = y + 0.5f;

        // The band is taken over the whole visible width, so every run of the device row finds its rows kept.
        int firstBandRow = ClampRow(Math.Min(GetRow(_visibleArea.Left + 0.5f, centerY), GetRow(_visibleArea.Right - 0.5f, centerY)));
        int lastBandRow = ClampRow(Math.Max(GetRow(_visibleArea.Left + 0.5f, centerY), GetRow(_visibleArea.Right - 0.5f, centerY)));

        _source.PrepareOutputRows(firstBandRow, lastBandRow);

        Vector2 position = _deviceToResized.MapVector2(new Vector2(left + 0.5f, centerY));
        Vector2 step = new(_deviceToResized.ScaleX, _deviceToResized.SkewY);

        for (int index = 0; index < count; index++)
        {
            int column = Math.Max(0, Math.Min(_resizedWidth - 1, (int)Math.Floor(position.X)));
            int row = Math.Max(firstBandRow, Math.Min(lastBandRow, (int)Math.Floor(position.Y)));

            _source.GetOutputRow(row).Slice(column * ChannelCount, ChannelCount).CopyTo(destination.Slice(index * ChannelCount, ChannelCount));
            position += step;
        }
    }

    private int GetRow(float x, float y) => (int)Math.Floor(_deviceToResized.MapVector2(new Vector2(x, y)).Y);

    private int ClampRow(int row) => Math.Max(0, Math.Min(_resizedHeight - 1, row));

    /// <inheritdoc />
    public override void Dispose() => _source.Dispose();
}
