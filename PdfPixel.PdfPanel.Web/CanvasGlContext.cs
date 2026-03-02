using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Encapsulates per-canvas EGL handles and the Skia GPU context.
/// EGL calls may be made from any thread; Emscripten proxies them transparently.
/// Skia operations (<see cref="GRContext"/>, <see cref="SKSurface"/>) require the browser main thread
/// and are marshalled accordingly.
/// </summary>
/// <remarks>
/// Before calling <see cref="CreateAsync"/>, ensure <c>Module["canvas"]</c> is set to the target
/// canvas element so that <c>eglCreateWindowSurface</c> binds to the correct WebGL context.
/// </remarks>
public sealed class CanvasGlContext : IDisposable
{
    private bool _disposed;

    internal CanvasGlContext(
    string canvasSelector,
    int webGlContext,
    GRContext grContext)
    {
        CanvasSelector = canvasSelector;
        WebGlContext = webGlContext;
        GrContext = grContext;
    }

    public string CanvasSelector { get; }

    public int WebGlContext { get; }

    /// <summary>Gets the Skia GPU context for this canvas.</summary>
    public GRContext GrContext { get; }

    /// <summary>
    /// Creates an <see cref="SKSurface"/> targeting framebuffer 0 of this canvas.
    /// Must be called on the dedicated render thread that owns the OffscreenCanvas.
    /// </summary>
    /// <param name="width">The surface width in pixels.</param>
    /// <param name="height">The surface height in pixels.</param>
    /// <param name="oldSurface">Old surface that existed before to be disposed.</param>
    /// <returns>A new <see cref="SKSurface"/> backed by this canvas's WebGL framebuffer.</returns>
    public SKSurface CreateSurface(int width, int height, SKSurface oldSurface = null)
    {
        Emscripten.WebGlMakeContextCurrent(WebGlContext);

        SKImage cpuSnapshot = null;
        if (oldSurface != null)
        {
            oldSurface.Flush();
            using var gpuSnapshot = oldSurface.Snapshot();
            cpuSnapshot = gpuSnapshot?.ToRasterImage();
        }

        Emscripten.SetCanvasSize(CanvasSelector, width, height);

        var glInfo = new GRGlFramebufferInfo(
            fboId: 0,
            format: 0x8058); // GL_RGBA8

        var renderTarget = new GRBackendRenderTarget(
            width,
            height,
            sampleCount: 0,
            stencilBits: 8,
            glInfo);

        var surface = SKSurface.Create(
            GrContext,
            renderTarget,
            GRSurfaceOrigin.BottomLeft,
            SKColorType.Rgba8888);

        if (surface == null)
        {
            throw new InvalidOperationException("Failed to create Skia surface for WebGL context.");
        }

        surface.Canvas.ClipRect(new SKRect(0, 0, width, height));

        if (cpuSnapshot != null)
        {
            surface.Canvas.DrawImage(cpuSnapshot, new SKPoint(0, 0));
            surface.Flush();
            cpuSnapshot.Dispose();
        }

        return surface;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Disposes the Skia GPU context and releases EGL handles.
    /// Should be called from the browser main thread so that <see cref="GRContext"/> can
    /// flush and release GPU resources while the context is current.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GrContext.Dispose();
        // TODO: destroy WebGlContext!!!
    }
}
