using PdfPixel.Geometry;
using PdfPixel.Snap.Rasterization;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// The device pixels an image covers, the same for color images and stencil masks: whole pixels when the image lies
/// along the device axes, otherwise its outline by pixel centers.
/// </summary>
internal readonly struct SnapImageFootprint
{
    private readonly PdfRectangle _footprint;
    private readonly PdfMatrix _unitToDevice;

    public SnapImageFootprint(PdfIntegerRectangle? pixels, in PdfRectangle footprint, in PdfMatrix unitToDevice)
    {
        Pixels = pixels;
        _footprint = footprint;
        _unitToDevice = unitToDevice;
    }

    /// <summary>
    /// Gets the whole pixels the image covers, or <see langword="null"/> when its outline is rasterized instead.
    /// </summary>
    public PdfIntegerRectangle? Pixels { get; }

    /// <summary>
    /// Replaces the content of <paramref name="edges"/> with the image's device outline.
    /// </summary>
    public void AddOutline(SnapEdgeList edges) => PdfSnapGrid.AddImageFootprint(edges, _footprint, _unitToDevice);

    /// <summary>
    /// Passes the footprint's coverage rows within <paramref name="limits"/> to <paramref name="sink"/>.
    /// </summary>
    public void Rasterize<TSink>(SnapScratchBuffer scratch, in PdfIntegerRectangle limits, ref TSink sink)
        where TSink : ISnapCoverageSink
    {
        if (Pixels != null)
        {
            scratch.Rasterizer.FillRectangle(Pixels.Value, limits, ref sink);
        }
        else
        {
            AddOutline(scratch.Edges);
            scratch.Rasterizer.Rasterize(scratch.Edges, PdfPathFillType.Winding, areaCoverage: false, limits, ref sink);
        }
    }
}
