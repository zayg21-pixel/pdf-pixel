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
    /// Returns the <see cref="SKSurface"/> of the canvas framebuffer (FBO 0) for the specified dimensions.
    /// The canvas is resized and a new surface is created only when the dimensions change.
    /// </summary>
    public SKSurface CreateSurface(int width, int height)
    {
        if (_presentSurface != null && _surfaceWidth == width && _surfaceHeight == height)
        {
            return _presentSurface;
        }

        EmscriptenInterop.WebGlMakeContextCurrent(WebGlContext);

        RecreatePresentSurface(width, height);

        _surfaceWidth = width;
        _surfaceHeight = height;

        return _presentSurface;
    }

    /// <summary>
    /// Flushes the canvas framebuffer surface for display.
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
        EmscriptenInterop.WebGlMakeContextCurrent(WebGlContext);
        _presentSurface?.Dispose();
        GrContext.Dispose();
        EmscriptenInterop.WebGlDestroyContext(WebGlContext);
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
