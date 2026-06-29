using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Web.Emscripten;
using SkiaSharp;
using System;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// CPU-backed Skia renderer for browser canvases. Uses a raster <see cref="SKSurface"/>,
/// reads its pixels and uploads them to the browser canvas via Emscripten.
/// Implements <see cref="IPdfPanelRenderTargetFactory"/> and <see cref="IPdfPanelRenderTarget"/>.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class CpuSkiaRenderer : ISkSurfaceFactory, IPdfPanelRenderTargetFactory, IPdfPanelRenderTarget
{
    private readonly string _canvasSelector;
    private readonly ILogger _logger;
    private readonly CpuSkSurfaceFactory _surfaceFactory;

    public CpuSkiaRenderer(ILogger logger, string canvasSelector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _canvasSelector = canvasSelector ?? throw new ArgumentNullException(nameof(canvasSelector));
        _surfaceFactory = new CpuSkSurfaceFactory(SKColorType.Rgba8888, SKAlphaType.Unpremul);
    }

    /// <inheritdoc />
    public void Initialize() => _surfaceFactory.Initialize();

    /// <inheritdoc />
    public SKSurface GetDrawingSurface(int width, int height)
    {
        // TODO: [HIGH] copy content from existing surface, add protected properties for surfaces
        SKSurface surface = _surfaceFactory.GetDrawingSurface(width, height);
        EmscriptenInterop.SetCanvasSize(_canvasSelector, width, height);
        return surface;
    }

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context) => this;

    /// <inheritdoc />
    public void Render(SKSurface surface, DrawingRequest request)
    {
        if (surface == null)
        {
            return;
        }

        surface.Canvas.Flush();

        using SKPixmap pixmap = surface.PeekPixels();

        if (pixmap == null)
        {
            return;
        }

        int width = pixmap.Width;
        int height = pixmap.Height;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        nint src = pixmap.GetPixels();

        try
        {
            EmscriptenInterop.SetCanvasRgba(_canvasSelector, src, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload CPU surface to canvas {CanvasSelector}", _canvasSelector);
        }
    }

    /// <inheritdoc />
    public SKSurface GetTilingSurface(int width, int height) => _surfaceFactory.GetTilingSurface(width, height);

    /// <inheritdoc />
    public void Dispose() => _surfaceFactory.Dispose();
}
