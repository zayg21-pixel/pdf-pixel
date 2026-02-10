using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Wpf.Drawing;
using SkiaSharp;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf
{
    public class SkiaPdfPanelRenderTargetFactory : IPdfPanelRenderTargetFactory
    {
        private readonly SkiaPdfPanel _panel;
        private SkiaPdfPanelRenderTarget _previousTarget;

        public SkiaPdfPanelRenderTargetFactory(SkiaPdfPanel panel)
        {
            _panel = panel;
        }

        public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var canvasSize = new SKSize((float)_panel.CanvasSize.Width, (float)_panel.CanvasSize.Height);
            var canvasScale = new SKPoint((float)_panel.CanvasScale.X, (float)_panel.CanvasScale.Y);
            var canvasOffset = new SKPoint((float)_panel.CanvasOffset.X, (float)_panel.CanvasOffset.Y);

            if (_previousTarget != null &&
                _previousTarget.CanvasSize == canvasSize &&
                _previousTarget.CanvasScale == canvasScale &&
                _previousTarget.CanvasOffset == canvasOffset)
            {
                return _previousTarget;
            }

            _previousTarget = GetNewRenderTarget(canvasSize, canvasScale, canvasOffset);

            return _previousTarget;
        }

        private SkiaPdfPanelRenderTarget GetNewRenderTarget(SKSize canvasSize, SKPoint canvasScale, SKPoint canvasOffset)
        {
            var writeableBitmap = new WriteableBitmap((int)canvasSize.Width, (int)canvasSize.Height, 96.0 * canvasScale.X, 96.0 * canvasScale.Y, PixelFormats.Pbgra32, null);
            return new SkiaPdfPanelRenderTarget(writeableBitmap, _panel, canvasSize, canvasScale, canvasOffset);
        }
    }

    partial class SkiaPdfPanelRenderTarget : IPdfPanelRenderTarget
    {
        public SkiaPdfPanelRenderTarget(WriteableBitmap writeableBitmap, SkiaPdfPanel panel, SKSize canvasSize, SKPoint canvasScale, SKPoint canvasOffset)
        {
            WriteableBitmap = writeableBitmap ?? throw new ArgumentNullException(nameof(writeableBitmap));
            Panel = panel ?? throw new ArgumentNullException(nameof(panel));
            CanvasSize = canvasSize;
            CanvasScale = canvasScale;
            CanvasOffset = canvasOffset;
        }

        public WriteableBitmap WriteableBitmap { get; }

        public SkiaPdfPanel Panel { get; }

        public SKSize CanvasSize { get; }

        public SKPoint CanvasScale { get; }

        public SKPoint CanvasOffset { get; }

        public async Task RenderAsync(SKSurface surface)
        {
            await Panel.Dispatcher.InvokeAsync(async () =>
            {
                DrawOnWritableBitmap(surface);
            });
        }

        private void DrawOnWritableBitmap(SKSurface surface)
        {
            if (WriteableBitmap.PixelWidth != surface.Canvas.DeviceClipBounds.Width || WriteableBitmap.PixelHeight != surface.Canvas.DeviceClipBounds.Height)
            {
                return;
            }

            WriteableBitmap.Lock();

            SKImageInfo imageInfo = new SKImageInfo(WriteableBitmap.PixelWidth, WriteableBitmap.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);

            surface.ReadPixels(imageInfo, WriteableBitmap.BackBuffer, WriteableBitmap.BackBufferStride, 0, 0);

            // TODO: Re-enable after testing
            //if (pagesDrawingRequest.Pages.OnAfterDraw != null)
            //{
            //    using SKSurface surface = SKSurface.Create(imageInfo, writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride);
            //    pagesDrawingRequest.Pages.OnAfterDraw?.Invoke(surface.Canvas, pagesDrawingRequest.VisiblePages, pagesDrawingRequest.Scale);
            //}

            WriteableBitmap.AddDirtyRect(new Int32Rect(0, 0, imageInfo.Width, imageInfo.Height));

            var drawingVisual = Panel.DrawingVisual;
            DrawingContext render = drawingVisual.RenderOpen();

            var pixelOffsetX = Panel.SnapPosition(CanvasOffset.X, CanvasScale.X);
            var pixelOffsetY = Panel.SnapPosition(CanvasOffset.Y, CanvasScale.Y);

            render.DrawImage(WriteableBitmap, new Rect(pixelOffsetX, pixelOffsetY, WriteableBitmap.Width, WriteableBitmap.Height));

            render.Close();

            WriteableBitmap.Unlock();
        }
    }
}