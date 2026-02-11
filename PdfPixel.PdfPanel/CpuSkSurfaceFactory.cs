using SkiaSharp;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Provides a factory for creating CPU-backed <see cref="SKSurface"/> instances.
/// </summary>
public class CpuSkSurfaceFactory : ISkSurfaceFactory
{
    /// <summary>
    /// Creates a CPU-backed <see cref="SKSurface"/> using the specified image information.
    /// </summary>
    /// <param name="imageInfo">The image information describing the surface properties.</param>
    /// <returns>A new CPU-backed <see cref="SKSurface"/> instance.</returns>
    public SKSurface GetSurface(SKImageInfo imageInfo)
    {
        return SKSurface.Create(imageInfo);
    }
}
