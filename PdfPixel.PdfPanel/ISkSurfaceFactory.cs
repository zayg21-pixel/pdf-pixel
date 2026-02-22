using SkiaSharp;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Defines a factory for creating <see cref="SKSurface"/> instances.
/// </summary>
public interface ISkSurfaceFactory : IDisposable
{
    /// <summary>
    /// Initializes the factory on the current thread. For WebGL/OffscreenCanvas,
    /// this creates the WebGL context (canvas must already be transferred by JS).
    /// </summary>
    void Initialize();

    /// <summary>
    /// Creates a new drawing <see cref="SKSurface"/> with the specified dimensions
    /// and disposes existing surface if any.
    /// </summary>
    /// <param name="width">Required surface width.</param>
    /// <param name="height">Required surface height.</param>
    /// <param name="token">Token to cancel surface request.</param>
    /// <returns>A new <see cref="SKSurface"/> instance.</returns>
    SKSurface GetDrawingSurface(int width, int height, CancellationToken token);

    /// <summary>
    /// Creates a <see cref="SKSurface"/> suitable for thumbnail rendering.
    /// The previous thumbnail surface is disposed on each call.
    /// </summary>
    /// <param name="width">Required surface width.</param>
    /// <param name="height">Required surface height.</param>
    /// <param name="token">Token to cancel surface request.</param>
    /// <returns>A new <see cref="SKSurface"/> instance.</returns>
    SKSurface CreateThumbnailSurface(int width, int height, CancellationToken token);

    /// <summary>
    /// Sets the specified surface as the current rendering target.
    /// For GPU-backed implementations, this ensures the correct graphics context is active.
    /// </summary>
    /// <param name="surface">The surface to make current.</param>
    void SetCurrentSurface(SKSurface surface);
}
