using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace PdfPixel.Wpf.DirectXExperiments
{
    /// <summary>
    /// Flushes the completed GPU frame to a <see cref="D3DImage"/>.
    /// The <see cref="SKSurface"/> passed to <see cref="RenderAsync"/> is the D3D12-backed GPU surface
    /// returned by <see cref="D3DImageRenderTargetFactory.GetDrawingSurface"/>, so no copy or snapshot is needed.
    /// </summary>
    internal sealed class D3DImageRenderTarget : IPdfPanelRenderTarget
    {
        private readonly D3DImage _d3dImage;

        internal D3DImageRenderTarget(D3DImage d3dImage)
        {
            _d3dImage = d3dImage;
        }

        /// <inheritdoc />
        public async Task RenderAsync(SKSurface surface, DrawingRequest request)
        {
            surface.Flush();
            await _d3dImage.Dispatcher.InvokeAsync(() =>
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
            });
        }
    }
}
