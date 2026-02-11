using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel;

public interface IPdfPanelRenderTarget
{
    Task RenderAsync(SKSurface surface, DrawingRequest request);
}
