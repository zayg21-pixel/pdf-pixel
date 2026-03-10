using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Encapsulates per-canvas WebGL handles, the Skia GPU context, and the paired
/// offscreen / present surfaces.
/// <para>
/// All rendering targets an offscreen GPU texture. <see cref="SKSurface.Snapshot"/>
/// on texture-backed surfaces evaluates eagerly via copy-on-write, unlike FBO 0
/// surfaces where it is lazily evaluated and unreliable. The offscreen content is
/// blitted to FBO 0 only when <see cref="Present"/> is called.
/// </para>
/// </summary>
/// <remarks>
/// Before calling <see cref="CreateAsync"/>, ensure <c>Module["canvas"]</c> is set to the target
/// canvas element so that <c>eglCreateWindowSurface</c> binds to the correct WebGL context.
/// </remarks>
public sealed class CanvasGlContext : IDisposable
{
    private bool _disposed;
    private readonly int _sampleCount;
    private SKSurface _offscreenSurface;
    private SKSurface _presentSurface;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private SKSurface _thumbnailSurface;
    private int _thumbnailWidth;
    private int _thumbnailHeight;

    internal CanvasGlContext(
    string canvasSelector,
    int webGlContext,
    GRContext grContext)
    {
        CanvasSelector = canvasSelector;
        WebGlContext = webGlContext;
        GrContext = grContext;
        _sampleCount = Math.Min(4, Math.Max(1, grContext.GetMaxSurfaceSampleCount(SKColorType.Rgba8888)));
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
    /// <param name="width">The surface width in pixels.</param>
    /// <param name="height">The surface height in pixels.</param>
    /// <param name="preserveContent">
    /// If <see langword="true"/> and a previous surface exists, its content is copied
    /// to the new surface via a GPU-to-GPU <c>DrawSurface</c> call.
    /// </param>
    /// <returns>The current offscreen <see cref="SKSurface"/> instance.</returns>
    public SKSurface CreateSurface(int width, int height, bool preserveContent)
    {
        if (_offscreenSurface != null && _surfaceWidth == width && _surfaceHeight == height)
        {
            return _offscreenSurface;
        }

        Emscripten.WebGlMakeContextCurrent(WebGlContext);

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var newSurface = SKSurface.Create(GrContext, false, info, _sampleCount, GRSurfaceOrigin.BottomLeft);

        if (newSurface == null)
        {
            throw new InvalidOperationException("Failed to create offscreen Skia surface for WebGL context.");
        }

        if (preserveContent && _offscreenSurface != null)
        {
            _offscreenSurface.Flush();
            newSurface.Canvas.DrawSurface(_offscreenSurface, 0, 0);
            newSurface.Flush();
        }

        _offscreenSurface?.Dispose();
        _offscreenSurface = newSurface;

        RecreatePresentSurface(width, height);

        _surfaceWidth = width;
        _surfaceHeight = height;

        return _offscreenSurface;
    }

    /// <summary>
    /// Returns an offscreen GPU texture-backed <see cref="SKSurface"/> for thumbnail rendering.
    /// Unlike <see cref="CreateSurface"/>, this does not create a companion FBO 0 present surface
    /// because thumbnails are only snapshotted, never displayed on a canvas.
    /// The surface shares the same <see cref="GRContext"/> as the main drawing surface, so
    /// <see cref="SKSurface.Snapshot"/> returns a GPU image that can be drawn directly on
    /// the main canvas without a slow <c>ToRasterImage</c> / <c>glReadPixels</c> round-trip.
    /// </summary>
    /// <param name="width">The surface width in pixels.</param>
    /// <param name="height">The surface height in pixels.</param>
    /// <returns>The current thumbnail <see cref="SKSurface"/> instance.</returns>
    public SKSurface CreateThumbnailSurface(int width, int height)
    {
        if (_thumbnailSurface != null && _thumbnailWidth == width && _thumbnailHeight == height)
        {
            return _thumbnailSurface;
        }

        Emscripten.WebGlMakeContextCurrent(WebGlContext);

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var newSurface = SKSurface.Create(GrContext, false, info, _sampleCount, GRSurfaceOrigin.BottomLeft);

        if (newSurface == null)
        {
            throw new InvalidOperationException("Failed to create offscreen thumbnail surface for WebGL context.");
        }

        _thumbnailSurface?.Dispose();
        _thumbnailSurface = newSurface;
        _thumbnailWidth = width;
        _thumbnailHeight = height;

        return _thumbnailSurface;
    }

    /// <summary>
    /// Blits the offscreen surface to FBO 0 for display.
    /// Must be called on the dedicated render thread that owns the OffscreenCanvas.
    /// </summary>
    public void Present()
    {
        if (_offscreenSurface == null || _presentSurface == null)
        {
            return;
        }

        Emscripten.WebGlMakeContextCurrent(WebGlContext);
        _offscreenSurface.Flush();
        _presentSurface.Canvas.DrawSurface(_offscreenSurface, 0, 0);
        _presentSurface.Flush();
    }

    /// <summary>
    /// Recreates the FBO 0 present surface and resizes the underlying canvas.
    /// </summary>
    /// <param name="width">Surface width in pixels.</param>
    /// <param name="height">Surface height in pixels.</param>
    private void RecreatePresentSurface(int width, int height)
    {
        _presentSurface?.Dispose();

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

    /// <inheritdoc />
    /// <remarks>
    /// Disposes the offscreen and present surfaces, then the Skia GPU context.
    /// Should be called from the render thread so that <see cref="GRContext"/> can
    /// flush and release GPU resources while the context is current.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _thumbnailSurface?.Dispose();
        _offscreenSurface?.Dispose();
        _presentSurface?.Dispose();
        GrContext.Dispose();
        // TODO: destroy WebGlContext!!!
    }
}
