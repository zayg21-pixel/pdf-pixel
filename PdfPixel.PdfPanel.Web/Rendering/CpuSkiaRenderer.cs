using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Web.Emscripten;
using SkiaSharp;
using System;
using System.Runtime.Versioning;
using System.Threading;

namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// CPU-backed Skia renderer for browser canvases. Uses a raster <see cref="SKSurface"/>,
/// reads its pixels and uploads them to the browser canvas via Emscripten.
/// Implements <see cref="IPdfPanelRenderTargetFactory"/> and <see cref="IPdfPanelRenderTarget"/>.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class CpuSkiaRenderer : CpuSkSurfaceFactory, IPdfPanelRenderTargetFactory, IPdfPanelRenderTarget
{
    private readonly string _canvasSelector;
    private readonly ILogger _logger;

    public CpuSkiaRenderer(ILogger logger, string canvasSelector)
        : base(SKColorType.Rgba8888, SKAlphaType.Unpremul)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _canvasSelector = canvasSelector ?? throw new ArgumentNullException(nameof(canvasSelector));
    }

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
    {
        return this;
    }

    public override SKSurface GetDrawingSurface(int width, int height, CancellationToken token)
    {
        // TODO: [HIGH] copy content from existing surface, add protected properties for surfaces
        var surface = base.GetDrawingSurface(width, height, token);
        EmscriptenInterop.SetCanvasSize(_canvasSelector, width, height);

        return surface;
    }

    /// <inheritdoc />
    public void Render(SKSurface surface, DrawingRequest request, CancellationToken token)
    {
        if (surface == null)
        {
            return;
        }

        // Ensure any pending drawing operations are flushed to the pixel buffer.
        surface.Canvas.Flush();

        using var pixmap = surface.PeekPixels();

        if (pixmap == null)
        {
            return;
        }

        var width = pixmap.Width;
        var height = pixmap.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var src = pixmap.GetPixels();

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
    public new void Dispose()
    {
        base.Dispose();
    }
}
