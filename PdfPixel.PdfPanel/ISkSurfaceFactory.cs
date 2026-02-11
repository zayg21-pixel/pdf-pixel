using SkiaSharp;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Defines a factory for creating <see cref="SKSurface"/> instances.
/// </summary>
public interface ISkSurfaceFactory
{
    /// <summary>
    /// Creates a new <see cref="SKSurface"/> with the specified image information.
    /// </summary>
    /// <param name="imageInfo">The image information describing the surface properties.</param>
    /// <returns>A new <see cref="SKSurface"/> instance.</returns>
    SKSurface GetSurface(SKImageInfo imageInfo);
}
