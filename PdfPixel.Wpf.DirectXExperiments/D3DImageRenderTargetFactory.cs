using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace PdfPixel.Wpf.DirectXExperiments
{
    /// <summary>
    /// Creates GPU-accelerated <see cref="IPdfPanelRenderTarget"/> instances backed by a <see cref="D3DImage"/>.
    /// Also implements <see cref="ISkSurfaceFactory"/> so the PDF rendering queue draws directly onto the
    /// D3D12-backed GPU surface — no CPU copy is needed.
    /// Manages the shared surface chain: D3D12 (SkiaSharp GPU) → D3D11 (bridge) → D3D9Ex (D3DImage interop).
    /// </summary>
    public sealed class D3DImageRenderTargetFactory : IPdfPanelRenderTargetFactory, IPdfPanelRenderTarget, ISkSurfaceFactory, IDisposable
    {
        private readonly object _lock = new object();
        private readonly D3DImage _d3dImage;
        private readonly Direct3DContext _d3dContext;
        private readonly GRContext _grContext;
        private readonly SharedDirectXResources _sharedResources;
        private D3D9Texture _currentTexture;
        private SKSurface _currentSurface;
        private SKSurface _currentThumbnailSurface;
        private bool _disposed;

        /// <summary>
        /// Initializes a new <see cref="D3DImageRenderTargetFactory"/> and creates all underlying DirectX devices.
        /// </summary>
        /// <param name="d3dImage">The WPF <see cref="D3DImage"/> that will display the rendered output.</param>
        public D3DImageRenderTargetFactory(D3DImage d3dImage)
        {
            _d3dImage = d3dImage ?? throw new ArgumentNullException(nameof(d3dImage));
            _d3dContext = Direct3DContext.Create();
            _grContext = GRContext.CreateDirect3D(_d3dContext.CreateBackendContext());
            _sharedResources = new SharedDirectXResources(_d3dContext);
        }

        /// <summary>
        /// Returns the GPU-backed <see cref="SKSurface"/> for the given dimensions.
        /// A new <see cref="D3D9Texture"/> and <see cref="SKSurface"/> are created, the D3DImage back buffer
        /// is updated atomically with already-drawn content, and only then are the old resources released.
        /// </summary>
        /// <inheritdoc />
        public async Task<SKSurface> GetDrawingSurfaceAsync(int width, int height)
        {
            D3D9Texture newTexture;
            SKSurface newSurface;
            D3D9Texture oldTexture = null;
            SKSurface oldSurface = null;

            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                newTexture = _sharedResources.CreateD3D9Texture(width, height);
                newSurface = _sharedResources.CreateSurface(newTexture, width, height, _grContext);
            }

            await _d3dImage.Dispatcher.InvokeAsync(() =>
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        newSurface?.Dispose();
                        newTexture?.Dispose();
                        return;
                    }

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
                }
            });

            lock (_lock)
            {
                if (_disposed)
                {
                    return null;
                }

                oldSurface?.Dispose();
                _grContext.PurgeResources();
                oldTexture?.Dispose();

                return _currentSurface;
            }
        }

        /// <inheritdoc />
        public async Task RenderAsync(SKSurface surface, DrawingRequest request)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }

            surface.Flush();

            await _d3dImage.Dispatcher.InvokeAsync(() =>
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }

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
                }
            });
        }

        /// <summary>
        /// Creates a GPU-backed offscreen surface for thumbnail rendering.
        /// Uses the existing <see cref="GRContext"/> directly — no shared D3D9/D3D11/D3D12 resources.
        /// </summary>
        /// <inheritdoc />
        public Task<SKSurface> CreateThumbnailSurfaceAsync(int width, int height)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var newSurface = SKSurface.Create(_grContext, false, info);

                var oldSurface = _currentThumbnailSurface;
                _currentThumbnailSurface = newSurface;

                oldSurface?.Dispose();
                _grContext.PurgeResources();

                return Task.FromResult(newSurface);
            }
        }

        /// <inheritdoc />
        public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
                return this;
            }
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

                _currentTexture?.Dispose();
                _currentTexture = null;

                _sharedResources?.Dispose();
                _grContext?.Dispose();
                _d3dContext?.Dispose();
            }
        }
    }
}
