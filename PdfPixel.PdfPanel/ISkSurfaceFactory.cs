using SkiaSharp;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Defines a factory for creating <see cref="SKSurface"/> instances.
/// </summary>
public interface ISkSurfaceFactory : IDisposable
{
    /// <summary>
    /// Creates a new drawing <see cref="SKSurface"/> with the specified dimensions
    /// and disposes existing surface if any.
    /// </summary>
    /// <param name="width">Required surface width.</param>
    /// <param name="height">Required surface height.</param>
    /// <returns>A new <see cref="SKSurface"/> instance.</returns>
    Task<SKSurface> GetDrawingSurfaceAsync(int width, int height);

    /// <summary>
    /// Creates a <see cref="SKSurface"/> suitable for thumbnail rendering.
    /// The previous thumbnail surface is disposed on each call.
    /// </summary>
    /// <param name="width">Required surface width.</param>
    /// <param name="height">Required surface height.</param>
    /// <returns>A new <see cref="SKSurface"/> instance.</returns>
    Task<SKSurface> CreateThumbnailSurfaceAsync(int width, int height);
}
