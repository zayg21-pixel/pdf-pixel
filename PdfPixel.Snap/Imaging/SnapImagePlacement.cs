using System;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// Maps one source of an image onto the device pixels it lands on.
/// </summary>
internal abstract class SnapImagePlacement : IDisposable
{
    protected SnapImagePlacement(int channelCount) => ChannelCount = channelCount;

    public int ChannelCount { get; }

    /// <summary>
    /// Writes the values of device row <paramref name="y"/> from column <paramref name="left"/>, <see cref="ChannelCount"/>
    /// per pixel.
    /// </summary>
    public abstract void ReadRow(int y, int left, in Span<float> destination);

    /// <inheritdoc />
    public abstract void Dispose();
}
