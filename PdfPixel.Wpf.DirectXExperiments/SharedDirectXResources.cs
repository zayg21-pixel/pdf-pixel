using SkiaSharp;
using System;
using Vortice.Direct3D11;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Direct3D9 = Vortice.Direct3D9;

namespace PdfPixel.Wpf.DirectXExperiments
{
    /// <summary>
    /// Stateless factory for creating the shared DirectX resource chain (D3D12 → D3D11 → D3D9) and
    /// the SkiaSharp <see cref="SKSurface"/> that draws into it.
    /// Callers own and dispose each returned object independently, which allows the D3DImage back buffer
    /// to be updated atomically with new content before old resources are released.
    /// </summary>
    internal class SharedDirectXResources : IDisposable
    {
        private readonly VorticeDirect3DContext _context;
        private bool _disposed;

        internal SharedDirectXResources(VorticeDirect3DContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Creates a new <see cref="D3D9Texture"/> for the given dimensions.
        /// The returned instance is owned by the caller and must be disposed when no longer needed.
        /// </summary>
        internal D3D9Texture CreateD3D9Texture(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Width and height must be positive.");
            }

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

            var d3d11RenderTarget = _context.D3D11Device.CreateTexture2D(d3d11TextureDesc);

            using var dxgiResource = d3d11RenderTarget.QueryInterface<IDXGIResource>();
            var sharedHandle = dxgiResource.SharedHandle;

            using var d3d12Device1 = _context.D3D12Device.QueryInterface<ID3D12Device1>();
            var d3d12RenderTarget = d3d12Device1.OpenSharedHandle<ID3D12Resource>(sharedHandle);

            var d3d9Surface = _context.D3D9Device.CreateRenderTarget(
                (uint)width,
                (uint)height,
                Direct3D9.Format.A8R8G8B8,
                Direct3D9.MultisampleType.None,
                0,
                false,
                ref sharedHandle);

            return new D3D9Texture(d3d12RenderTarget, d3d11RenderTarget, d3d9Surface);
        }

        /// <summary>
        /// Creates an <see cref="SKSurface"/> backed by the D3D12 resource inside <paramref name="texture"/>.
        /// The returned surface is owned by the caller and must be disposed when no longer needed.
        /// </summary>
        internal SKSurface CreateSurface(D3D9Texture texture, int width, int height, GRContext grContext)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (grContext == null)
            {
                throw new ArgumentNullException(nameof(grContext));
            }

            var renderTargetInfo = new GRD3DTextureResourceInfo
            {
                Resource = texture.D3D12ResourcePointer,
                ResourceState = (uint)ResourceStates.RenderTarget,
                Format = (uint)Format.B8G8R8A8_UNorm,
                SampleCount = 1,
                LevelCount = 1,
                SampleQualityPattern = 0,
                Protected = false
            };

            using var backendRenderTarget = new GRBackendRenderTarget(width, height, renderTargetInfo);

            return SKSurface.Create(
                grContext,
                backendRenderTarget,
                GRSurfaceOrigin.TopLeft,
                SKColorType.Bgra8888);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
    }
}
