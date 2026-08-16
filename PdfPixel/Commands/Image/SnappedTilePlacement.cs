using PdfPixel.Geometry;

namespace PdfPixel.Commands.Image;

internal readonly struct SnappedTilePlacement
{
    public SnappedTilePlacement(in PdfSize deviceSize, in PdfMatrix placementMatrix, bool interpolate)
    {
        DeviceSize = deviceSize;
        PlacementMatrix = placementMatrix;
        Interpolate = interpolate;
    }

    public PdfSize DeviceSize { get; }

    public PdfMatrix PlacementMatrix { get; }

    public PdfRectangle PlacementRectangle => new(0, 0, DeviceSize.Width, DeviceSize.Height);

    public bool Interpolate { get; }
}
