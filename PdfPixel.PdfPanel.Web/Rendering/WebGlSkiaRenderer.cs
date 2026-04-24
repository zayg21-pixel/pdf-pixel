using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Web.Emscripten;
using SkiaSharp;
using System;
using System.Runtime.Versioning;
using System.Threading;

namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// Implements <see cref="IPdfPanelRenderTargetFactory"/>, <see cref="ISkSurfaceFactory"/>,
/// and <see cref="IPdfPanelRenderTarget"/> for a single WebGL-backed canvas.
/// The drawing surface is an offscreen GPU texture; at present time it is blitted to FBO 0.
/// Canvas transfer is handled by JS during panel registration.
/// All methods are called from the render thread - no locking required.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class WebGlSkiaRenderer : IPdfPanelRenderTargetFactory, ISkSurfaceFactory, IPdfPanelRenderTarget
{
    private readonly string _canvasSelector;
    private readonly ILogger _logger;
    private CanvasGlContext _glContext;
    private int _currentWebGlContext;

    public WebGlSkiaRenderer(ILogger logger, string canvasSelector)
    {
        _logger = logger;
        _canvasSelector = canvasSelector;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Creates the WebGL context for the main canvas. Canvas transfer is handled by JS
    /// during panel registration before this method is called.
    /// </remarks>
    public void Initialize()
    {
        _glContext = CreateGlContext(_canvasSelector);
    }

    /// <summary>
    /// Creates a WebGL context and GRContext for the specified canvas selector.
    /// </summary>
    /// <param name="canvasSelector">The canvas selector for which to create the context.</param>
    /// <returns>A new <see cref="CanvasGlContext"/> instance.</returns>
    private CanvasGlContext CreateGlContext(string canvasSelector)
    {
        _logger.LogInformation("Creating WebGL context for {CanvasSelector}", canvasSelector);

        var webglCtx = EmscriptenInterop.WebGlCreateContext(
            canvasId: canvasSelector,
            alpha: 1,
            depth: 1,
            stencil: 1,
            antialias: 0,
            majorVersion: 2);

        if (webglCtx <= 0)
        {
            _logger.LogError("WebGlCreateContext failed for {CanvasSelector}: {Result}", canvasSelector, webglCtx);
            throw new InvalidOperationException($"WebGlCreateContext failed for {canvasSelector}: {webglCtx}");
        }

        var result = EmscriptenInterop.WebGlMakeContextCurrent(webglCtx);
        if (result != 0)
        {
            _logger.LogError("WebGlMakeContextCurrent failed for {CanvasSelector}: {Result}", canvasSelector, result);
            throw new InvalidOperationException($"WebGlMakeContextCurrent failed for {canvasSelector}: {result}");
        }

        _currentWebGlContext = webglCtx;

        _logger.LogInformation("WebGL context {Context} made current for {CanvasSelector}", webglCtx, canvasSelector);

        using var glInterface = GRGlInterface.Create();
        if (glInterface == null)
        {
            _logger.LogError("Failed to create GRGlInterface for {CanvasSelector}", canvasSelector);
            throw new InvalidOperationException($"Failed to create GRGlInterface for {canvasSelector}");
        }

        var grContext = GRContext.CreateGl(glInterface);
        if (grContext == null)
        {
            _logger.LogError("Failed to create GRContext for {CanvasSelector}", canvasSelector);
            throw new InvalidOperationException($"Failed to create GRContext for {canvasSelector}");
        }

        _logger.LogInformation("GRContext created for {CanvasSelector}", canvasSelector);

        return new CanvasGlContext(canvasSelector, webglCtx, grContext);
    }

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
    {
        return this;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns an offscreen GPU texture-backed <see cref="SKSurface"/>.
    /// Delegates to <see cref="CanvasGlContext.CreateSurface"/> which manages the
    /// offscreen surface and FBO 0 present surface internally.
    /// Makes the WebGL context current before returning.
    /// </remarks>
    public SKSurface GetDrawingSurface(int width, int height, CancellationToken token)
    {
        if (_glContext == null)
        {
            throw new InvalidOperationException("Initialize must be called before GetDrawingSurface");
        }

        MakeContextCurrent(_glContext.WebGlContext);
        return _glContext.CreateSurface(width, height, preserveContent: true);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns an offscreen GPU texture-backed <see cref="SKSurface"/> for thumbnail rendering.
    /// Uses the main <see cref="CanvasGlContext"/> so that <see cref="SKSurface.Snapshot"/> returns
    /// a GPU image on the same <see cref="GRContext"/> as the main drawing surface, allowing it
    /// to be drawn directly without a slow <c>ToRasterImage</c> / <c>glReadPixels</c> call.
    /// </remarks>
    public SKSurface GetThumbnailSurface(int width, int height, CancellationToken token)
    {
        if (_glContext == null)
        {
            throw new InvalidOperationException("Initialize must be called before GetThumbnailSurface");
        }

        MakeContextCurrent(_glContext.WebGlContext);
        return _glContext.CreateThumbnailSurface(width, height);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Blits the offscreen surface to FBO 0 for display via <see cref="CanvasGlContext.Present"/>.
    /// Must be called on the dedicated render thread that owns the OffscreenCanvas.
    /// </remarks>
    public void Render(SKSurface surface, DrawingRequest request, CancellationToken token)
    {
        if (surface == null)
        {
            return;
        }

        MakeContextCurrent(_glContext.WebGlContext);
        _glContext.Present();
    }

    /// <summary>
    /// Makes the specified WebGL context current on this thread, skipping the native call
    /// when it is already current. Safe to call from multiple <see cref="WebGlSkiaRenderer"/>
    /// instances because <see cref="_currentWebGlContext"/> is shared across all of them.
    /// </summary>
    /// <param name="webGlContext">The WebGL context handle to make current.</param>
    private void MakeContextCurrent(int webGlContext)
    {
        if (_currentWebGlContext == webGlContext)
        {
            return;
        }

        EmscriptenInterop.WebGlMakeContextCurrent(webGlContext);
        _currentWebGlContext = webGlContext;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Called from the render thread via DisposeRequest.
    /// Disposes GL contexts (which dispose their owned surfaces).
    /// </remarks>
    public void Dispose()
    {
        _glContext?.Dispose();
    }
}
