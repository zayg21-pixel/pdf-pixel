using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using WebGL.Sample;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Implements <see cref="IPdfPanelRenderTargetFactory"/>, <see cref="ISkSurfaceFactory"/>,
/// and <see cref="IPdfPanelRenderTarget"/> for a single WebGL-backed canvas.
/// The drawing surface is the WebGL framebuffer itself; rendering is a plain Skia flush
/// dispatched to the browser main thread.
/// Thumbnail surfaces are CPU-backed since they are off-screen. //TODO: create thumbnail surfaces on the GPU as well, and read back the pixels to the CPU for display in the UI.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class WebGlSkiaRenderer : IPdfPanelRenderTargetFactory, ISkSurfaceFactory, IPdfPanelRenderTarget
{
    private readonly CanvasGlContext _glContext;
    private SKSurface _currentSurface;
    private SKSurface _currentThumbnailSurface;

    public WebGlSkiaRenderer(CanvasGlContext glContext)
    {
        _glContext = glContext;
    }

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context) => this;

    /// <inheritdoc />
    /// <remarks>
    /// Creates a GPU-backed <see cref="SKSurface"/> targeting framebuffer 0 of the WebGL canvas.
    /// The previous surface is disposed before the new one is created.
    /// </remarks>
    public async Task<SKSurface> GetDrawingSurfaceAsync(int width, int height)
    {
        SKImage snapshot = null;
        if (_currentSurface != null)
        {
            snapshot = await Emscripten.RunOnMainThreadAsync(_currentSurface.Snapshot);
        }

        var oldSurface = _currentSurface;
        _currentSurface = await _glContext.CreateSurfaceAsync(width, height, snapshot);
        oldSurface?.Dispose();
        snapshot?.Dispose();
        return _currentSurface;
    }

    /// <inheritdoc />
    /// <remarks>Thumbnail surfaces are CPU-backed and do not require a GPU context.</remarks>
    public Task<SKSurface> CreateThumbnailSurfaceAsync(int width, int height)
    {
        _currentThumbnailSurface?.Dispose();
        _currentThumbnailSurface = SKSurface.Create(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        return Task.FromResult(_currentThumbnailSurface);
    }

    /// <inheritdoc />
    /// <remarks>
    /// For a WebGL-backed surface the canvas is updated by flushing the Skia command buffer.
    /// The flush must run on the browser main thread.
    /// </remarks>
    public Task RenderAsync(SKSurface surface, DrawingRequest request)
    {
        return Emscripten.RunOnMainThreadAsync(surface.Flush);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _currentSurface?.Dispose();
        _currentSurface = null;
        _currentThumbnailSurface?.Dispose();
        _currentThumbnailSurface = null;
        _glContext.Dispose();
    }
}
