using PdfRender.Canvas;
using System.Runtime.Versioning;

namespace PdfRender.Web.PdfPanel
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