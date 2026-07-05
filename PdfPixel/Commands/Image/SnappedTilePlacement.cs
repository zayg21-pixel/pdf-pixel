using SkiaSharp;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Where a tile should be drawn: the device-pixel size its shader should be built for, the
/// matrix to concat onto the canvas (in place of the usual pixel-space scale/translate) to
/// place it there, and the sampling options to use.
/// </summary>
internal readonly struct SnappedTilePlacement
{
    public SnappedTilePlacement(SKSizeI deviceSize, SKMatrix placementMatrix, in SKSamplingOptions sampling)
    {
        DeviceSize = deviceSize;
        PlacementMatrix = placementMatrix;
        PlacementRectangle = new SKRect(0, 0, deviceSize.Width, deviceSize.Height);
        Sampling = sampling;
    }

    public SKSizeI DeviceSize { get; }

    public SKMatrix PlacementMatrix { get; }

    public SKRect PlacementRectangle { get; }

    public SKSamplingOptions Sampling { get; }
}
