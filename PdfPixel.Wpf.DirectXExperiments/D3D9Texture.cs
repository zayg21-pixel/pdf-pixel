using PdfPixel.Wpf.DirectXExperiments.Interop;
using System;

namespace PdfPixel.Wpf.DirectXExperiments
{
    /// <summary>
    /// Disposable wrapper over the D3D12, D3D11, and D3D9 resource handles that back a single render surface.
    /// Does not own the <see cref="SkiaSharp.SKSurface"/> — that is created separately via
    /// <see cref="SharedDirectXResources.CreateSurface"/> and has an independent lifetime.
    /// </summary>
    internal sealed class D3D9Texture : IDisposable
    {
        private readonly DirectXHandle _d3d12RenderTarget;
        private readonly DirectXHandle _d3d11RenderTarget;
        private readonly DirectXHandle _d3d9Surface;
        private bool _disposed;

        internal D3D9Texture(
            DirectXHandle d3d12RenderTarget,
            DirectXHandle d3d11RenderTarget,
            DirectXHandle d3d9Surface)
        {
            _d3d12RenderTarget = d3d12RenderTarget;
            _d3d11RenderTarget = d3d11RenderTarget;
            _d3d9Surface = d3d9Surface;
        }

        /// <summary>Native pointer of the D3D9 surface, used as the <see cref="System.Windows.Interop.D3DImage"/> back buffer.</summary>
        internal IntPtr D3D9SurfacePointer => _d3d9Surface.Ptr;

        /// <summary>
        /// Native pointer of the D3D12 resource, used to create the SkiaSharp <see cref="SkiaSharp.GRBackendRenderTarget"/>.
        /// </summary>
        internal IntPtr D3D12ResourcePointer => _d3d12RenderTarget.Ptr;

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
