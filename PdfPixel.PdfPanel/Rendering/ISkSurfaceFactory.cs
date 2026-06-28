using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Creates and caches <see cref="SKSurface"/> instances for rendering.
/// Surfaces are reused when dimensions are unchanged; a new surface is created only on resize.
/// </summary>
public interface ISkSurfaceFactory : IDisposable
{
    /// <summary>
    /// Performs any platform-specific initialisation. Must be called once before <see cref="GetDrawingSurface"/>.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Returns the drawing <see cref="SKSurface"/> for the specified pixel dimensions.
    /// Previous content is preserved when the size changes.
    /// </summary>
    SKSurface GetDrawingSurface(int width, int height);

    /// <summary>
    /// Returns a reusable scratch surface for off-screen tile rasterization.
    /// The surface is cleared and reused for each tile. Only recreated on size change.
    /// </summary>
    SKSurface GetTilingSurface(int width, int height);
}

