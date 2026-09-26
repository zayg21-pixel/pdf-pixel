using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
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
    private readonly Action<PdfPanelFrame> _onFramePresented;
    private int _canvasWidth;
    private int _canvasHeight;

    public CpuSkiaRenderer(ILogger logger, string canvasSelector, Action<PdfPanelFrame> onFramePresented)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _canvasSelector = canvasSelector ?? throw new ArgumentNullException(nameof(canvasSelector));
        _onFramePresented = onFramePresented ?? throw new ArgumentNullException(nameof(onFramePresented));
        _surfaceFactory = new CpuSkSurfaceFactory(SKColorType.Rgba8888, SKAlphaType.Unpremul);
    }

    /// <inheritdoc />
    public void Initialize() => _surfaceFactory.Initialize();

    /// <inheritdoc />
    public SKSurface GetDrawingSurface(int width, int height) => _surfaceFactory.GetDrawingSurface(width, height);

    /// <inheritdoc />
    public PdfMatrix HostToPanel => PdfMatrix.Identity;

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context) => this;

    /// <inheritdoc />
    public void Render(SKSurface surface, PdfPanelFrame frame)
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
            if (width != _canvasWidth || height != _canvasHeight)
            {
                EmscriptenInterop.SetCanvasSize(_canvasSelector, width, height);
                _canvasWidth = width;
                _canvasHeight = height;
            }

            EmscriptenInterop.SetCanvasRgba(_canvasSelector, src, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload CPU surface to canvas {CanvasSelector}", _canvasSelector);
            return;
        }

        _onFramePresented(frame);
    }

    /// <inheritdoc />
    public SKSurface GetTilingSurface(int width, int height) => _surfaceFactory.GetTilingSurface(width, height);

    /// <inheritdoc />
    public void Dispose() => _surfaceFactory.Dispose();
}
