using SkiaSharp;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Defines a factory for creating and caching <see cref="SKSurface"/> instances.
/// Implementations track the current surface dimensions internally and only
/// recreate a surface when the requested size changes.
/// </summary>
public interface ISkSurfaceFactory : IDisposable
{
    /// <summary>
    /// Initializes the factory on the current thread. For WebGL/OffscreenCanvas,
    /// this creates the WebGL context (canvas must already be transferred by JS).
    /// </summary>
    void Initialize();

    /// <summary>
    /// Returns the drawing <see cref="SKSurface"/> for the specified dimensions.
    /// The factory tracks the current size and only creates a new surface when the
    /// dimensions change. The caller should request the surface each time it is needed
    /// rather than caching the returned instance.
    /// </summary>
    /// <param name="width">Required surface width.</param>
    /// <param name="height">Required surface height.</param>
    /// <param name="token">Token to cancel surface request.</param>
    /// <returns>The current drawing <see cref="SKSurface"/> instance.</returns>
    SKSurface GetDrawingSurface(int width, int height, CancellationToken token);

    /// <summary>
    /// Returns the thumbnail <see cref="SKSurface"/> for the specified dimensions.
    /// The factory tracks the current size and only creates a new surface when the
    /// dimensions change. The caller should request the surface each time it is needed
    /// rather than caching the returned instance.
    /// </summary>
    /// <param name="width">Required surface width.</param>
    /// <param name="height">Required surface height.</param>
    /// <param name="token">Token to cancel surface request.</param>
    /// <returns>The current thumbnail <see cref="SKSurface"/> instance.</returns>
    SKSurface GetThumbnailSurface(int width, int height, CancellationToken token);
}
