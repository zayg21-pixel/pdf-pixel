using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Runtime.Versioning;
using System.Threading;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Implements <see cref="IPdfPanelRenderTargetFactory"/>, <see cref="ISkSurfaceFactory"/>,
/// and <see cref="IPdfPanelRenderTarget"/> for a single WebGL-backed canvas.
/// The drawing surface is the WebGL framebuffer itself; rendering is a plain Skia flush.
/// Canvas transfer is handled by JS during panel registration.
/// All methods are called from the render thread - no locking required.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class WebGlSkiaRenderer : IPdfPanelRenderTargetFactory, ISkSurfaceFactory, IPdfPanelRenderTarget
{
    private readonly string _canvasSelector;
    private readonly string _thumbnailCanvasSelector;
    private readonly ILogger _logger;
    private CanvasGlContext _glContext;
    private CanvasGlContext _thumbnailGlContext;
    private SKSurface _currentSurface;
    private SKSurface _currentThumbnailSurface;

    public WebGlSkiaRenderer(ILogger logger, string canvasSelector, string thumbnailCanvasSelector)
    {
        _logger = logger;
        _canvasSelector = canvasSelector;
        _thumbnailCanvasSelector = thumbnailCanvasSelector;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Creates the WebGL contexts for both canvases. Canvas transfer is handled by JS
    /// during panel registration before this method is called.
    /// </remarks>
    public void Initialize()
    {
        _glContext = CreateGlContext(_canvasSelector);
        _thumbnailGlContext = CreateGlContext(_thumbnailCanvasSelector);
    }

    /// <summary>
    /// Creates a WebGL context and GRContext for the specified canvas selector.
    /// </summary>
    /// <param name="canvasSelector">The canvas selector for which to create the context.</param>
    /// <returns>A new <see cref="CanvasGlContext"/> instance.</returns>
    private CanvasGlContext CreateGlContext(string canvasSelector)
    {
        _logger.LogInformation("Creating WebGL context for {CanvasSelector}", canvasSelector);

        var webglCtx = Emscripten.WebGlCreateContext(
            canvasId: canvasSelector,
            alpha: 1,
            depth: 1,
            stencil: 1,
            antialias: 1,
            majorVersion: 2);

        if (webglCtx <= 0)
        {
            _logger.LogError("WebGlCreateContext failed for {CanvasSelector}: {Result}", canvasSelector, webglCtx);
            throw new InvalidOperationException($"WebGlCreateContext failed for {canvasSelector}: {webglCtx}");
        }

        var result = Emscripten.WebGlMakeContextCurrent(webglCtx);
        if (result != 0)
        {
            _logger.LogError("WebGlMakeContextCurrent failed for {CanvasSelector}: {Result}", canvasSelector, result);
            throw new InvalidOperationException($"WebGlMakeContextCurrent failed for {canvasSelector}: {result}");
        }

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
    /// Creates a GPU-backed <see cref="SKSurface"/> targeting framebuffer 0 of the WebGL canvas.
    /// The previous surface is disposed before the new one is created.
    /// Updates the OffscreenCanvas size if the dimensions have changed.
    /// </remarks>
    public SKSurface GetDrawingSurface(int width, int height, CancellationToken token)
    {
        return CreateSurface(_glContext, ref _currentSurface, width, height, preserveContent: true, nameof(GetDrawingSurface));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Creates a GPU-backed <see cref="SKSurface"/> targeting framebuffer 0 of the thumbnail WebGL canvas.
    /// The previous surface is disposed before the new one is created.
    /// Updates the OffscreenCanvas size if the dimensions have changed.
    /// </remarks>
    public SKSurface CreateThumbnailSurface(int width, int height, CancellationToken token)
    {
        return CreateSurface(_thumbnailGlContext, ref _currentThumbnailSurface, width, height, preserveContent: false, nameof(CreateThumbnailSurface));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Makes the WebGL context current for the specified surface.
    /// Must be called before any drawing operations on the surface.
    /// </remarks>
    public void SetCurrentSurface(SKSurface surface)
    {
        CanvasGlContext context = null;

        if (surface == _currentSurface)
        {
            context = _glContext;
        }
        else if (surface == _currentThumbnailSurface)
        {
            context = _thumbnailGlContext;
        }

        if (context != null)
        {
            Emscripten.WebGlMakeContextCurrent(context.WebGlContext);
        }
    }

    /// <summary>
    /// Creates a GPU-backed surface for the specified GL context.
    /// </summary>
    /// <param name="glContext">The GL context to create the surface for.</param>
    /// <param name="currentSurface">Reference to the current surface field.</param>
    /// <param name="width">Surface width in pixels.</param>
    /// <param name="height">Surface height in pixels.</param>
    /// <param name="preserveContent">If true, copies old surface content to new surface.</param>
    /// <param name="callerName">Name of the calling method for error messages.</param>
    private SKSurface CreateSurface(CanvasGlContext glContext, ref SKSurface currentSurface, int width, int height, bool preserveContent, string callerName)
    {
        if (glContext == null)
        {
            throw new InvalidOperationException($"Initialize must be called before {callerName}");
        }

        var oldSurface = currentSurface;
        var newSurface = glContext.CreateSurface(width, height, preserveContent ? oldSurface : null);
        oldSurface?.Dispose();

        currentSurface = newSurface;
        return currentSurface;
    }

    /// <inheritdoc />
    /// <remarks>
    /// For a WebGL-backed surface the canvas is updated by flushing the Skia command buffer
    /// and committing the frame (required when using explicitSwapControl).
    /// Must be called on the dedicated render thread that owns the OffscreenCanvas.
    /// </remarks>
    public void Render(SKSurface surface, DrawingRequest request, CancellationToken token)
    {
        if (surface == null)
        {
            return;
        }

        SetCurrentSurface(surface);
        surface.Flush();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Called from the render thread via DisposeRequest.
    /// Disposes GL contexts - surfaces are disposed by PdfRenderingQueue.
    /// </remarks>
    public void Dispose()
    {
        _glContext?.Dispose();
        _thumbnailGlContext?.Dispose();
    }
}
