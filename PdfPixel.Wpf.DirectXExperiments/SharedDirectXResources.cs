using SkiaSharp;
using System;
using Vortice.Direct3D11;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Direct3D9 = Vortice.Direct3D9;

namespace PdfPixel.Wpf.DirectXExperiments
{
    /// <summary>
    /// Helper class for creating shared DirectX resources across D3D12, D3D11, and D3D9.
    /// Manages the bridging between different DirectX APIs for GPU-accelerated rendering with D3DImage.
    /// </summary>
    public class SharedDirectXResources : IDisposable
    {
        private readonly VorticeDirect3DContext _context;
        private ID3D12Resource _d3d12RenderTarget;
        private ID3D11Texture2D _d3d11RenderTarget;
        private Direct3D9.IDirect3DSurface9 _d3d9Surface;
        private SKSurface _skSurface;
        private bool _disposed;

        public SharedDirectXResources(VorticeDirect3DContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public SKSurface SkiaSurface => _skSurface;

        public Direct3D9.IDirect3DSurface9 D3D9Surface => _d3d9Surface;

        /// <summary>
        /// Creates shared DirectX resources for the specified dimensions.
        /// The resources are shared across D3D12 (for SkiaSharp GPU rendering), D3D11 (as bridge), and D3D9 (for D3DImage).
        /// </summary>
        public void CreateSharedResources(int width, int height, GRContext grContext)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Width and height must be positive");
            }

            if (grContext == null)
            {
                throw new ArgumentNullException(nameof(grContext));
            }

            DisposeResources();

            var d3d11TextureDesc = new Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.Shared
            };

            _d3d11RenderTarget = _context.D3D11Device.CreateTexture2D(d3d11TextureDesc);

            using var dxgiResource = _d3d11RenderTarget.QueryInterface<IDXGIResource>();
            var sharedHandle = dxgiResource.SharedHandle;

            var d3d12Device1 = _context.D3D12Device.QueryInterface<ID3D12Device1>();
            _d3d12RenderTarget = d3d12Device1.OpenSharedHandle<ID3D12Resource>(sharedHandle);
            d3d12Device1.Dispose();

            var renderTargetInfo = new GRD3DTextureResourceInfo
            {
                Resource = _d3d12RenderTarget.NativePointer,
                ResourceState = (uint)ResourceStates.RenderTarget,
                Format = (uint)Format.B8G8R8A8_UNorm,
                SampleCount = 1,
                LevelCount = 1,
                SampleQualityPattern = 0,
                Protected = false
            };

            var backendRenderTarget = new GRBackendRenderTarget(
                width,
                height,
                renderTargetInfo);

            _skSurface = SKSurface.Create(
                grContext,
                backendRenderTarget,
                GRSurfaceOrigin.TopLeft,
                SKColorType.Bgra8888);

            _d3d9Surface = _context.D3D9Device.CreateRenderTarget(
                (uint)width,
                (uint)height,
                Direct3D9.Format.A8R8G8B8,
                Direct3D9.MultisampleType.None,
                0,
                false,
                ref sharedHandle);
        }

        private void DisposeResources()
        {
            _skSurface?.Dispose();
            _skSurface = null;

            _d3d9Surface?.Dispose();
            _d3d9Surface = null;

            _d3d11RenderTarget?.Dispose();
            _d3d11RenderTarget = null;

            _d3d12RenderTarget?.Dispose();
            _d3d12RenderTarget = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeResources();
        }
    }
}
