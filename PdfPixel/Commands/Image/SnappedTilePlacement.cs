using SkiaSharp;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Where a tile should be drawn: the device-pixel size its shader should be built for, the
/// matrix to concat onto the canvas (in place of the usual pixel-space scale/translate) to
/// place it there, whether the following clip should be antialiased, and the sampling options
/// to use. Produced by <see cref="PdfImageCommandUtilities.GetSnappedTilePlacement"/>.
/// </summary>
internal readonly struct SnappedTilePlacement
{
    public SnappedTilePlacement(SKSizeI deviceSize, SKMatrix placementMatrix, bool isAntialiased, in SKSamplingOptions sampling)
    {
        DeviceSize = deviceSize;
        PlacementMatrix = placementMatrix;
        PlacementRectangle = new SKRect(0, 0, deviceSize.Width, deviceSize.Height);
        IsAntialiased = isAntialiased;
        Sampling = sampling;
    }

    public SKSizeI DeviceSize { get; }

    public SKMatrix PlacementMatrix { get; }

    public SKRect PlacementRectangle { get; }

    public bool IsAntialiased { get; }

    public SKSamplingOptions Sampling { get; }
}
