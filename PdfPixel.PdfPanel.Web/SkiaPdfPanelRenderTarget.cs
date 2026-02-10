using PdfPixel.PdfPanel;
using SkiaSharp;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace PdfPixel.Web.PdfPanel
{
    [SupportedOSPlatform("browser")]
    partial class SkiaPdfPanelRenderTarget : ICanvasRenderTarget
    {
        private readonly string _canvasId;

        public SkiaPdfPanelRenderTarget(string canvasId)
        {
            _canvasId = canvasId;
        }

        public async Task RenderAsync(SKSurface surface)
        {
            surface.Canvas.Flush();

            await UiInvoker.InvokeAsync(() =>
            {
                var imageInfo = new SKImageInfo(surface.Canvas.DeviceClipBounds.Width, surface.Canvas.DeviceClipBounds.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var bitmap = new SKBitmap(imageInfo);
                surface.ReadPixels(imageInfo, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
                var rgbaBytes = bitmap.Bytes;

                PdfPanelIntrop.JSRenderRgbaToCanvas(_canvasId, surface.Canvas.DeviceClipBounds.Width, surface.Canvas.DeviceClipBounds.Height, rgbaBytes);
            });
        }
    }
}