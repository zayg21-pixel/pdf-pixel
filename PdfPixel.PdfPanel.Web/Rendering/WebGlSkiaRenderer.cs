using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Web.Emscripten;
using SkiaSharp;
using System;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// Implements <see cref="IPdfPanelRenderTargetFactory"/>, <see cref="ISkSurfaceFactory"/>,
/// and <see cref="IPdfPanelRenderTarget"/> for a single WebGL-backed canvas.
/// The drawing surface is the canvas framebuffer (FBO 0).
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class WebGlSkiaRenderer : IPdfPanelRenderTargetFactory, ISkSurfaceFactory, IPdfPanelRenderTarget
{
    private readonly string _canvasSelector;
    private readonly ILogger _logger;
    private readonly Action<PdfPanelFrame> _onFramePresented;
    private CanvasGlContext _glContext;
    private SKSurface _tilingSurface;
    private int _tilingWidth;
    private int _tilingHeight;

    public WebGlSkiaRenderer(ILogger logger, string canvasSelector, Action<PdfPanelFrame> onFramePresented)
    {
        _logger = logger;
        _canvasSelector = canvasSelector;
        _onFramePresented = onFramePresented ?? throw new ArgumentNullException(nameof(onFramePresented));
    }

    /// <inheritdoc />
    public void Initialize()
    {
        if (_glContext != null)
        {
            return;
        }

        _glContext = CreateGlContext(_canvasSelector);
    }

    /// <inheritdoc />
    public PdfMatrix HostToPanel => PdfMatrix.Identity;

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context) => this;

    /// <inheritdoc />
    public SKSurface GetDrawingSurface(int width, int height)
    {
        if (_glContext == null)
        {
            throw new InvalidOperationException("Initialize must be called before GetDrawingSurface");
        }

        EmscriptenInterop.WebGlMakeContextCurrent(_glContext.WebGlContext);
        return _glContext.CreateSurface(width, height);
    }

    /// <inheritdoc />
    public void Render(SKSurface surface, PdfPanelFrame frame)
    {
        if (surface == null)
        {
            return;
        }

        _glContext.Present();
        _onFramePresented(frame);
    }

    /// <inheritdoc />
    public SKSurface GetTilingSurface(int width, int height)
    {
        EmscriptenInterop.WebGlMakeContextCurrent(_glContext.WebGlContext);

        if (_tilingSurface != null && _tilingWidth == width && _tilingHeight == height)
        {
            return _tilingSurface;
        }

        _tilingSurface?.Dispose();
        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _tilingSurface = SKSurface.Create(_glContext.GrContext, budgeted: true, info);
        _tilingWidth = width;
        _tilingHeight = height;

        return _tilingSurface;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_glContext != null)
        {
            EmscriptenInterop.WebGlMakeContextCurrent(_glContext.WebGlContext);
        }

        _tilingSurface?.Dispose();
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
}
