using SkiaSharp;
using System;
using System.Threading.Tasks;
using WebGL.Sample;

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
    private readonly EglGlobalContext _globalContext;
    private readonly IntPtr _eglContext;
    private readonly IntPtr _eglSurface;
    private bool _disposed;

    /// <summary>Gets the Skia GPU context for this canvas.</summary>
    public GRContext GrContext { get; }

    private CanvasGlContext(
        EglGlobalContext globalContext,
        IntPtr eglContext,
        IntPtr eglSurface,
        GRContext grContext)
    {
        _globalContext = globalContext;
        _eglContext = eglContext;
        _eglSurface = eglSurface;
        GrContext = grContext;
    }

    /// <summary>
    /// Creates a new <see cref="CanvasGlContext"/> for the currently targeted canvas.
    /// EGL object creation runs on any thread (Emscripten proxies to main).
    /// Skia object creation is explicitly marshalled to the browser main thread.
    /// </summary>
    /// <param name="globalContext">The shared EGL display and configuration.</param>
    /// <returns>A fully initialized <see cref="CanvasGlContext"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if any EGL or Skia initialization step fails.</exception>
    public static async Task<CanvasGlContext> CreateAsync(EglGlobalContext globalContext)
    {
        // EGL object creation — proxied to main thread by Emscripten; no explicit marshal required.
        int[] ctxAttribs = [EGL.EGL_CONTEXT_CLIENT_VERSION, 3, EGL.EGL_NONE];
        var eglContext = EGL.CreateContext(
            globalContext.Display,
            globalContext.Config,
            (IntPtr)EGL.EGL_NO_CONTEXT,
            ctxAttribs);

        if (eglContext == IntPtr.Zero)
            throw new InvalidOperationException("EGL.CreateContext() failed.");

        var eglSurface = EGL.CreateWindowSurface(
            globalContext.Display,
            globalContext.Config,
            IntPtr.Zero,
            null);

        if (eglSurface == IntPtr.Zero)
            throw new InvalidOperationException("EGL.CreateWindowSurface() failed.");

        if (!EGL.MakeCurrent(globalContext.Display, eglSurface, eglSurface, eglContext))
            throw new InvalidOperationException("EGL.MakeCurrent() failed.");

        // Skia object creation must run on the browser main thread.
        var grContext = await Emscripten.RunOnMainThreadAsync(() =>
        {
            var glInterface = GRGlInterface.CreateWebGl(EGL.GetProcAddress);
            if (glInterface == null)
                throw new InvalidOperationException("GRGlInterface.CreateWebGl() failed.");

            return GRContext.CreateGl(glInterface);
        });

        return new CanvasGlContext(globalContext, eglContext, eglSurface, grContext);
    }

    /// <summary>
    /// Creates an <see cref="SKSurface"/> targeting framebuffer 0 of this canvas.
    /// Must be called on the browser main thread.
    /// </summary>
    /// <param name="width">The surface width in pixels.</param>
    /// <param name="height">The surface height in pixels.</param>
    /// <param name="snapshot">Snapshot to draw on canvas on context recreation.</param>
    /// <returns>A new <see cref="SKSurface"/> backed by this canvas's WebGL framebuffer.</returns>
    public Task<SKSurface> CreateSurfaceAsync(int width, int height, SKImage snapshot = null)
    {
        return Emscripten.RunOnMainThreadAsync(() =>
        {
            var glInfo = new GRGlFramebufferInfo(
                fboId: 0,
                format: 0x8058); // GL_RGBA8

            // FBO 0 in WebGL is always single-sample from the application's perspective.
            // Browser-side MSAA (requested via EGL_SAMPLES) is resolved transparently by the
            // browser before compositing; Skia must not see a non-zero sample count here, or it
            // will resolve to its own internal FBO and leave FBO 0 cleared after every flush.
            var renderTarget = new GRBackendRenderTarget(
                width,
                height,
                sampleCount: 0,
                _globalContext.StencilBits,
                glInfo);

            var surface = SKSurface.Create(
                GrContext,
                renderTarget,
                GRSurfaceOrigin.BottomLeft,
                SKColorType.Rgba8888);

            if (snapshot != null)
            {
                surface.Canvas.DrawImage(snapshot, new SKPoint(0, 0));
            }

            return surface;
        });
    }

    /// <summary>
    /// Makes this canvas's EGL context and surface current for subsequent GL/Skia operations.
    /// May be called from any thread.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if <c>eglMakeCurrent</c> fails.</exception>
    public void MakeCurrent()
    {
        if (!EGL.MakeCurrent(_globalContext.Display, _eglSurface, _eglSurface, _eglContext))
            throw new InvalidOperationException("EGL.MakeCurrent() failed.");
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
        _ = EGL.DestroySurface(_globalContext.Display, _eglSurface);
        _ = EGL.DestroyContext(_globalContext.Display, _eglContext);
    }
}
