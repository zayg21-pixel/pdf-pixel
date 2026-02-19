using PdfPixel.PdfPanel;
using SkiaSharp;
using System;
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
    public sealed class D3DImageRenderTargetFactory : IPdfPanelRenderTargetFactory, ISkSurfaceFactory, IDisposable
    {
        private readonly D3DImage _d3dImage;
        private readonly VorticeDirect3DContext _d3dContext;
        private readonly GRContext _grContext;
        private readonly SharedDirectXResources _sharedResources;
        private readonly D3DImageRenderTarget _renderTarget;
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
            _d3dContext = VorticeDirect3DContext.Create();
            _grContext = GRContext.CreateDirect3D(_d3dContext.CreateBackendContext());
            _sharedResources = new SharedDirectXResources(_d3dContext);
            _renderTarget = new D3DImageRenderTarget(_d3dImage);
        }

        /// <summary>
        /// Returns the GPU-backed <see cref="SKSurface"/> for the given dimensions.
        /// A new <see cref="D3D9Texture"/> and <see cref="SKSurface"/> are created, the D3DImage back buffer
        /// is updated atomically with already-drawn content, and only then are the old resources released.
        /// </summary>
        /// <inheritdoc />
        public SKSurface GetDrawingSurface(int width, int height)
        {
            var newTexture = _sharedResources.CreateD3D9Texture(width, height);
            var newSurface = _sharedResources.CreateSurface(newTexture, width, height, _grContext);

            _d3dImage.Dispatcher.Invoke(() =>
            {
                _d3dImage.Lock();
                try
                {
                    // SetBackBuffer and AddDirtyRect are in the same Lock/Unlock so WPF never
                    // composites an empty surface. The snapshot is flushed to the GPU before
                    // AddDirtyRect so the first presented frame already has content.
                    _d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, newTexture.D3D9Surface.NativePointer);

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
            });

            _currentSurface?.Dispose();
            _grContext.PurgeResources();
            _currentTexture?.Dispose();

            _currentTexture = newTexture;
            _currentSurface = newSurface;

            return newSurface;
        }

        /// <summary>
        /// Creates a GPU-backed offscreen surface for thumbnail rendering.
        /// Uses the existing <see cref="GRContext"/> directly — no shared D3D9/D3D11/D3D12 resources.
        /// </summary>
        /// <inheritdoc />
        public SKSurface CreateThumbnailSurface(int width, int height)
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var newSurface = SKSurface.Create(_grContext, false, info);

            _currentThumbnailSurface?.Dispose();
            _grContext.PurgeResources();
            _currentThumbnailSurface = newSurface;

            return newSurface;
        }

        /// <inheritdoc />
        public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return _renderTarget;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _currentSurface?.Dispose();
            _currentThumbnailSurface?.Dispose();
            _currentTexture?.Dispose();
            _sharedResources?.Dispose();
            _grContext?.Dispose();
            _d3dContext?.Dispose();
        }
    }
}
