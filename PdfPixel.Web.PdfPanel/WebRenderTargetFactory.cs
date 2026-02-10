using PdfPixel.Canvas;
using System.Runtime.Versioning;

namespace PdfPixel.Web.PdfPanel
{
    [SupportedOSPlatform("browser")]
    internal class WebRenderTargetFactory : ICanvasRenderTargetFactory
    {
        private readonly string _canvasId;

        public WebRenderTargetFactory(string canvasId)
        {
            _canvasId = canvasId;
        }

        public ICanvasRenderTarget GetRenderTarget(PdfViewerCanvas renderCanvas)
        {
            return new SkiaPdfPanelRenderTarget(_canvasId);
        }
    }
}