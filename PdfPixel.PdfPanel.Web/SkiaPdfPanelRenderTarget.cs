using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Web;

[SupportedOSPlatform("browser")]
partial class SkiaPdfPanelRenderTarget : IPdfPanelRenderTarget
{
    private readonly string _canvasId;

    public SkiaPdfPanelRenderTarget(string canvasId)
    {
        _canvasId = canvasId;
    }

    public async Task RenderAsync(SKSurface surface, DrawingRequest request)
    {
        surface.Canvas.Flush();

        await UiInvoker.InvokeAsync(() =>
        {
            var imageInfo = new SKImageInfo(surface.Canvas.DeviceClipBounds.Width, surface.Canvas.DeviceClipBounds.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(imageInfo);
            surface.ReadPixels(imageInfo, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
            var rgbaBytes = bitmap.Bytes;

            PdfPanelInterop.JSRenderRgbaToCanvas(_canvasId, surface.Canvas.DeviceClipBounds.Width, surface.Canvas.DeviceClipBounds.Height, rgbaBytes);
        });
    }
}