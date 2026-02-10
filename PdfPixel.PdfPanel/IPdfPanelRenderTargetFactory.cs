namespace PdfPixel.PdfPanel;

public interface IPdfPanelRenderTargetFactory
{
    IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext panel);
}
