using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using WebGL.Sample;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Implements <see cref="IPdfPanelRenderTargetFactory"/>, <see cref="ISkSurfaceFactory"/>,
/// and <see cref="IPdfPanelRenderTarget"/> for a single WebGL-backed canvas.
/// The drawing surface is the WebGL framebuffer itself; rendering is a plain Skia flush
/// dispatched to the browser main thread.
/// Thumbnail surfaces are GPU-backed using a dedicated off-screen WebGL canvas. //TODO: create thumbnail surfaces on the GPU as well, and read back the pixels to the CPU for display in the UI. GPU readback is handled transparently by <see cref="SkiaSharp.SKSurface.Snapshot"/>.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class WebGlSkiaRenderer : IPdfPanelRenderTargetFactory, ISkSurfaceFactory, IPdfPanelRenderTarget
{
    private readonly object _lock = new object();
    private readonly CanvasGlContext _glContext;
    private readonly CanvasGlContext _thumbnailGlContext;
    private SKSurface _currentSurface;
    private SKSurface _currentThumbnailSurface;
    private bool _disposed;

    public WebGlSkiaRenderer(CanvasGlContext glContext, CanvasGlContext thumbnailGlContext)
    {
        _glContext = glContext;
        _thumbnailGlContext = thumbnailGlContext;
    }

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return this;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Creates a GPU-backed <see cref="SKSurface"/> targeting framebuffer 0 of the WebGL canvas.
    /// The previous surface is disposed before the new one is created.
    /// </remarks>
    public async Task<SKSurface> GetDrawingSurfaceAsync(int width, int height)
    {
        SKSurface oldSurface;
        SKSurface newSurface;

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            oldSurface = _currentSurface;
        }

        newSurface = await _glContext.CreateSurfaceAsync(width, height, oldSurface);

        lock (_lock)
        {
            if (_disposed)
            {
                newSurface?.Dispose();
                return null;
            }

            _currentSurface = newSurface;
            return _currentSurface;
        }
    }

    /// <inheritdoc />
    /// <remarks>Thumbnail surfaces are GPU-backed using the dedicated off-screen thumbnail WebGL canvas.</remarks>
    public async Task<SKSurface> CreateThumbnailSurfaceAsync(int width, int height)
    {
        SKSurface oldSurface;

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            oldSurface = _currentThumbnailSurface;
        }

        var newSurface = await _thumbnailGlContext.CreateSurfaceAsync(width, height, oldSurface);

        lock (_lock)
        {
            if (_disposed)
            {
                newSurface?.Dispose();
                return null;
            }

            _currentThumbnailSurface = newSurface;
            return _currentThumbnailSurface;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// For a WebGL-backed surface the canvas is updated by flushing the Skia command buffer.
    /// The flush must run on the browser main thread.
    /// </remarks>
    public Task RenderAsync(SKSurface surface, DrawingRequest request)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        return Emscripten.RunOnMainThreadAsync(() =>
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                Emscripten.WebGlMakeContextCurrent(_glContext.WebGlContext);
                surface.Flush();
            }
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _currentSurface?.Dispose();
            _currentSurface = null;

            _currentThumbnailSurface?.Dispose();
            _currentThumbnailSurface = null;

            _glContext?.Dispose();
            _thumbnailGlContext?.Dispose();
        }
    }
}
