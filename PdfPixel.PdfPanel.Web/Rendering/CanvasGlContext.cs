using PdfPixel.PdfPanel.Web.Emscripten;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Encapsulates per-canvas WebGL handles.
/// </summary>
public sealed class CanvasGlContext : IDisposable
{
    private bool _disposed;
    private SKSurface _presentSurface;
    private int _surfaceWidth;
    private int _surfaceHeight;

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
    /// Returns an offscreen GPU texture-backed <see cref="SKSurface"/> for the specified
    /// dimensions. A new surface is only created when the dimensions change.
    /// Optionally preserves content from the previous surface on resize.
    /// Also manages a companion FBO 0 present surface for display via <see cref="Present"/>.
    /// Must be called on the dedicated render thread that owns the OffscreenCanvas.
    /// </summary>
    public SKSurface CreateSurface(int width, int height, bool preserveContent)
    {
        if (_presentSurface != null && _surfaceWidth == width && _surfaceHeight == height)
        {
            return _presentSurface;
        }

        EmscriptenInterop.WebGlMakeContextCurrent(WebGlContext);

        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        SKSurface newSurface = SKSurface.Create(GrContext, budgeted: true, info, sampleCount: 1, GRSurfaceOrigin.BottomLeft);

        if (newSurface == null)
        {
            throw new InvalidOperationException("Failed to create offscreen Skia surface for WebGL context.");
        }

        if (preserveContent && _presentSurface != null)
        {
            _presentSurface.Flush();
            newSurface.Canvas.DrawSurface(_presentSurface, 0, 0);
            newSurface.Flush();
        }

        _presentSurface?.Dispose();
        _presentSurface = newSurface;

        RecreatePresentSurface(width, height);

        _surfaceWidth = width;
        _surfaceHeight = height;

        return _presentSurface;
    }

    /// <summary>
    /// Blits the offscreen surface to FBO 0 for display.
    /// Must be called on the dedicated render thread that owns the OffscreenCanvas.
    /// </summary>
    public void Present()
    {
        if (_presentSurface == null)
        {
            return;
        }

        EmscriptenInterop.WebGlMakeContextCurrent(WebGlContext);
        _presentSurface.Flush();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _presentSurface?.Dispose();
        GrContext.Dispose();
        // TODO: destroy WebGlContext!!!
    }

    private void RecreatePresentSurface(int width, int height)
    {
        _presentSurface?.Dispose();

        EmscriptenInterop.SetCanvasSize(CanvasSelector, width, height);

        GRGlFramebufferInfo glInfo = new(
            fboId: 0,
            format: 0x8058); // GL_RGBA8

        GRBackendRenderTarget renderTarget = new(
            width,
            height,
            sampleCount: 0,
            stencilBits: 8,
            glInfo);

        _presentSurface = SKSurface.Create(
            GrContext,
            renderTarget,
            GRSurfaceOrigin.BottomLeft,
            SKColorType.Rgba8888);

        if (_presentSurface == null)
        {
            throw new InvalidOperationException("Failed to create FBO 0 present surface for WebGL context.");
        }

        _presentSurface.Canvas.ClipRect(new SKRect(0, 0, width, height));
    }
}
