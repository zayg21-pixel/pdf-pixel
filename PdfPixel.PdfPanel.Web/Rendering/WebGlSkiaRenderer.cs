using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Web.Emscripten;
using SkiaSharp;
using System;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// Implements <see cref="IPdfPanelRenderTargetFactory"/>, <see cref="ISkSurfaceFactory"/>,
/// and <see cref="IPdfPanelRenderTarget"/> for a single WebGL-backed canvas.
/// The drawing surface is an offscreen GPU texture; at present time it is blitted to FBO 0.
/// Canvas transfer is handled by JS during panel registration.
/// All methods are called from the render thread — no locking required.
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
    public void Initialize()
    {
        _glContext = CreateGlContext(_canvasSelector);
    }

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context) => this;

    /// <inheritdoc />
    public SKSurface GetDrawingSurface(int width, int height)
    {
        if (_glContext == null)
        {
            throw new InvalidOperationException("Initialize must be called before GetDrawingSurface");
        }

        MakeContextCurrent(_glContext.WebGlContext);
        return _glContext.CreateSurface(width, height, preserveContent: true);
    }

    /// <inheritdoc />
    public void Render(SKSurface surface, DrawingRequest request)
    {
        if (surface == null)
        {
            return;
        }

        MakeContextCurrent(_glContext.WebGlContext);
        _glContext.Present();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _glContext?.Dispose();
    }

    private CanvasGlContext CreateGlContext(string canvasSelector)
    {
        _logger.LogInformation("Creating WebGL context for {CanvasSelector}", canvasSelector);

        int webglCtx = EmscriptenInterop.WebGlCreateContext(
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

        int result = EmscriptenInterop.WebGlMakeContextCurrent(webglCtx);
        if (result != 0)
        {
            _logger.LogError("WebGlMakeContextCurrent failed for {CanvasSelector}: {Result}", canvasSelector, result);
            throw new InvalidOperationException($"WebGlMakeContextCurrent failed for {canvasSelector}: {result}");
        }

        _currentWebGlContext = webglCtx;

        _logger.LogInformation("WebGL context {Context} made current for {CanvasSelector}", webglCtx, canvasSelector);

        using GRGlInterface glInterface = GRGlInterface.Create();
        if (glInterface == null)
        {
            _logger.LogError("Failed to create GRGlInterface for {CanvasSelector}", canvasSelector);
            throw new InvalidOperationException($"Failed to create GRGlInterface for {canvasSelector}");
        }

        GRContext grContext = GRContext.CreateGl(glInterface);
        if (grContext == null)
        {
            _logger.LogError("Failed to create GRContext for {CanvasSelector}", canvasSelector);
            throw new InvalidOperationException($"Failed to create GRContext for {canvasSelector}");
        }

        _logger.LogInformation("GRContext created for {CanvasSelector}", canvasSelector);

        return new CanvasGlContext(canvasSelector, webglCtx, grContext);
    }

    private void MakeContextCurrent(int webGlContext)
    {
        if (_currentWebGlContext == webGlContext)
        {
            return;
        }

        EmscriptenInterop.WebGlMakeContextCurrent(webGlContext);
        _currentWebGlContext = webGlContext;
    }
}
