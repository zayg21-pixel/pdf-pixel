using SkiaSharp;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Defines a factory for creating <see cref="SKSurface"/> instances.
/// </summary>
public interface ISkSurfaceFactory
{
    /// <summary>
    /// Creates a new <see cref="SKSurface"/> with the specified dimensions.
    /// </summary>
    /// <param name="width">Required surface width.</param>
    /// <param name="width">Required surface height.</param>
    /// <returns>A new <see cref="SKSurface"/> instance.</returns>
    SKSurface GetSurface(int width, int height);
}
