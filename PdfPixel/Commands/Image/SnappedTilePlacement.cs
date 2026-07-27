using PdfPixel.Geometry;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Where a tile should be drawn: the device-pixel size it should be rendered at, the matrix to
/// concatenate onto the current transformation (in place of the usual pixel-space scale/translate)
/// to place it there, and whether it should be interpolated when drawn.
/// </summary>
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
