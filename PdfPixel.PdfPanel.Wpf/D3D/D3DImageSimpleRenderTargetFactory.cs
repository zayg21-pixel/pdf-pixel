using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace PdfPixel.PdfPanel.Wpf.D3D;

/// <summary>
/// Creates GPU-accelerated <see cref="IPdfPanelRenderTarget"/> instances backed by a <see cref="D3DImage"/>.
/// Also implements <see cref="ISkSurfaceFactory"/> so the PDF rendering queue draws directly onto the
/// D3D12-backed GPU surface — no CPU copy is needed.
/// Manages the shared surface chain: D3D12 (SkiaSharp GPU) → D3D11 (bridge) → D3D9Ex (D3DImage interop).
/// <para>
/// This is a simplified variant of <see cref="D3DImageRenderTargetFactory"/> that draws directly
/// onto the D3D9-backed surface without a separate presentation surface or MSAA.
/// </para>
/// </summary>
public sealed class D3DImageSimpleRenderTargetFactory : IPdfPanelRenderTargetFactory, IPdfPanelRenderTarget, ISkSurfaceFactory, IDisposable
{
    /// <summary>
    /// Maximum GPU resource cache size in bytes. Limits stencil/texture memory growth
    /// from complex path operations (clipping, masking).
    /// </summary>
    private const long ResourceCacheLimitBytes = 128_000_000;

    private readonly D3DImage _d3dImage;
    private readonly Direct3DContext _d3dContext;
    private readonly GRContext _grContext;
    private readonly SharedDirectXResources _sharedResources;
    private D3D9Texture _currentTexture;
    private SKSurface _currentSurface;
    private SKSurface _currentThumbnailSurface;
    private int _currentWidth;
    private int _currentHeight;
    private int _currentThumbnailWidth;
    private int _currentThumbnailHeight;

    /// <summary>
    /// Initializes a new <see cref="D3DImageSimpleRenderTargetFactory"/> and creates all underlying DirectX devices.
    /// Drawing happens directly on the single-sample D3D9-backed surface — no MSAA or intermediate
    /// presentation surface is used.
    /// </summary>
    /// <param name="d3dImage">The WPF <see cref="D3DImage"/> that will display the rendered output.</param>
    public D3DImageSimpleRenderTargetFactory(D3DImage d3dImage)
    {
        _d3dImage = d3dImage ?? throw new ArgumentNullException(nameof(d3dImage));
        _d3dContext = Direct3DContext.Create();
        _grContext = GRContext.CreateDirect3D(_d3dContext.CreateBackendContext(), new GRContextOptions { RuntimeProgramCacheSize = 128_000_000 });

        // Cap GPU resource cache to limit stencil/texture memory from path clipping.
        _grContext.SetResourceCacheLimit(ResourceCacheLimitBytes);

        _sharedResources = new SharedDirectXResources(_d3dContext);
    }

    /// <inheritdoc />
    public void Initialize()
    {
        // no op
    }

    /// <summary>
    /// Returns the GPU-backed <see cref="SKSurface"/> for the given dimensions.
    /// A new <see cref="D3D9Texture"/> and <see cref="SKSurface"/> are created only when the
    /// dimensions change. The D3DImage back buffer is updated atomically with already-drawn
    /// content, and only then are the old resources released.
    /// Drawing occurs directly on the D3D9-backed surface — no separate presentation surface is used.
    /// </summary>
    /// <inheritdoc />
    public SKSurface GetDrawingSurface(int width, int height, CancellationToken token)
    {
        if (_currentSurface != null && _currentWidth == width && _currentHeight == height)
        {
            return _currentSurface;
        }

        D3D9Texture newTexture;
        SKSurface newSurface;
        D3D9Texture oldTexture = null;
        SKSurface oldSurface = null;

        _d3dImage.Dispatcher.Invoke(() =>
        {
            newTexture = _sharedResources.CreateD3D9Texture(width, height);
            newSurface = _sharedResources.CreateSurface(newTexture, width, height, _grContext);
            newSurface.Canvas.ClipRect(new SKRect(0, 0, width, height));

            _d3dImage.Lock();
            try
            {
                // SetBackBuffer and AddDirtyRect are in the same Lock/Unlock so WPF never
                // composites an empty surface. The snapshot is flushed to the GPU before
                // AddDirtyRect so the first presented frame already has content.
                _d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, newTexture.D3D9SurfacePointer);

                if (_currentSurface != null)
                {
                    _currentSurface.Flush();
                    newSurface.Canvas.DrawSurface(_currentSurface, SKPoint.Empty);
                    newSurface.Flush();

                    _d3dImage.AddDirtyRect(new Int32Rect(0, 0, width, height));
                }
            }
            finally
            {
                _d3dImage.Unlock();
            }

            oldSurface = _currentSurface;
            oldTexture = _currentTexture;

            _currentTexture = newTexture;
            _currentSurface = newSurface;
        }, System.Windows.Threading.DispatcherPriority.Render, token);

        _currentWidth = width;
        _currentHeight = height;

        oldSurface?.Dispose();
        oldTexture?.Dispose();

        return _currentSurface;
    }

    /// <summary>
    /// Returns a GPU-backed offscreen surface for thumbnail rendering.
    /// Uses the existing <see cref="GRContext"/> directly — no shared D3D9/D3D11/D3D12 resources.
    /// Only creates a new surface when the dimensions change.
    /// </summary>
    /// <inheritdoc />
    public SKSurface GetThumbnailSurface(int width, int height, CancellationToken token)
    {
        if (_currentThumbnailSurface != null && _currentThumbnailWidth == width && _currentThumbnailHeight == height)
        {
            return _currentThumbnailSurface;
        }

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var newSurface = SKSurface.Create(_grContext, budgeted: true, info, sampleCount: 1);
        newSurface.Canvas.ClipRect(new SKRect(0, 0, width, height));

        var oldSurface = _currentThumbnailSurface;
        _currentThumbnailSurface = newSurface;
        _currentThumbnailWidth = width;
        _currentThumbnailHeight = height;

        oldSurface?.Dispose();

        return newSurface;
    }

    /// <inheritdoc />
    public void Render(SKSurface surface, DrawingRequest request, CancellationToken token)
    {
        surface.Flush();
        _grContext.Flush();

        _d3dImage.Dispatcher.Invoke(() =>
        {
            var bounds = surface.Canvas.DeviceClipBounds;

            _d3dImage.Lock();
            try
            {
                _d3dImage.AddDirtyRect(new Int32Rect(0, 0, bounds.Width, bounds.Height));
            }
            finally
            {
                _d3dImage.Unlock();
            }
        }, System.Windows.Threading.DispatcherPriority.Render, token);
    }

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
    {
        return this;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _currentSurface?.Dispose();
        _currentSurface = null;

        _currentThumbnailSurface?.Dispose();
        _currentThumbnailSurface = null;

        _currentTexture?.Dispose();
        _currentTexture = null;

        _sharedResources?.Dispose();
        _grContext?.Dispose();
        _d3dContext?.Dispose();
    }
}
