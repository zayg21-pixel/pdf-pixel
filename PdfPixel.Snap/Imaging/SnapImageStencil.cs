using PdfPixel.Snap.Rasterization;
using System;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// The coverage of a stencil mask (ISO 32000-2 8.9.6.2) over its image's footprint: the share of each pixel's samples
/// that paint.
/// </summary>
internal abstract class SnapImageStencil : IDisposable
{
    public abstract void Rasterize<TSink>(ref TSink sink)
        where TSink : ISnapCoverageSink;

    /// <inheritdoc />
    public abstract void Dispose();
}
