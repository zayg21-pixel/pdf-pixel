using System;
using Vortice.Direct3D11;
using Vortice.Direct3D12;
using Direct3D9 = Vortice.Direct3D9;

namespace PdfPixel.Wpf.DirectXExperiments
{
    /// <summary>
    /// Disposable wrapper over the D3D12, D3D11, and D3D9 resources that back a single render surface.
    /// Does not own the <see cref="SkiaSharp.SKSurface"/> — that is created separately via
    /// <see cref="SharedDirectXResources.CreateSurface"/> and has an independent lifetime.
    /// </summary>
    internal sealed class D3D9Texture : IDisposable
    {
        private readonly ID3D12Resource _d3d12RenderTarget;
        private readonly ID3D11Texture2D _d3d11RenderTarget;
        private readonly Direct3D9.IDirect3DSurface9 _d3d9Surface;
        private bool _disposed;

        public D3D9Texture(
            ID3D12Resource d3d12RenderTarget,
            ID3D11Texture2D d3d11RenderTarget,
            Direct3D9.IDirect3DSurface9 d3d9Surface)
        {
            _d3d12RenderTarget = d3d12RenderTarget;
            _d3d11RenderTarget = d3d11RenderTarget;
            _d3d9Surface = d3d9Surface;
        }

        /// <summary>The D3D9 surface used as the <see cref="System.Windows.Interop.D3DImage"/> back buffer.</summary>
        public Direct3D9.IDirect3DSurface9 D3D9Surface => _d3d9Surface;

        /// <summary>
        /// Native pointer of the D3D12 resource, used to create the SkiaSharp <see cref="SkiaSharp.GRBackendRenderTarget"/>.
        /// </summary>
        public IntPtr D3D12ResourcePointer => _d3d12RenderTarget.NativePointer;

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _d3d9Surface?.Dispose();
            _d3d11RenderTarget?.Dispose();
            _d3d12RenderTarget?.Dispose();
        }
    }
}
