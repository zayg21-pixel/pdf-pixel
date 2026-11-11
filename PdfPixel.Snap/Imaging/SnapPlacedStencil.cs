using PdfPixel.Geometry;
using PdfPixel.Snap.Rasterization;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// A stencil mask read through an image placement: the image footprint's pixels, each covered by the resized stencil
/// coverage the placement gives it.
/// </summary>
internal sealed class SnapPlacedStencil : SnapImageStencil
{
    private readonly SnapImagePlacement _coverage;
    private readonly SnapImageFootprint _footprint;
    private readonly PdfIntegerRectangle _limits;
    private readonly SnapScratchBuffer _scratch;

    public SnapPlacedStencil(SnapImagePlacement coverage, in SnapImageFootprint footprint, in PdfIntegerRectangle limits, SnapScratchBuffer scratch)
    {
        _coverage = coverage;
        _footprint = footprint;
        _limits = limits;
        _scratch = scratch;
    }

    /// <inheritdoc />
    public override void Rasterize<TSink>(ref TSink sink)
    {
        SnapStencilCoverageSink<TSink> stencilSink = new(_coverage, _limits.Width, sink);

        _footprint.Rasterize(_scratch, _limits, ref stencilSink);
    }

    /// <inheritdoc />
    public override void Dispose() => _coverage.Dispose();
}
