using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web;

[SupportedOSPlatform("browser")]
internal class WebRenderTargetFactory : IPdfPanelRenderTargetFactory
{
    private readonly string _canvasId;

    public WebRenderTargetFactory(string canvasId)
    {
        _canvasId = canvasId;
    }

    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
    {
        return new SkiaPdfPanelRenderTarget(_canvasId);
    }
}