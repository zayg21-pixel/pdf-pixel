using SkiaSharp;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf
{
    public class WpfPdfPanelRenderTargetFactory : IPdfPanelRenderTargetFactory
    {
        private readonly WpfPdfPanel _panel;
        private WpfPdfPanelRenderTarget _previousTarget;

        public WpfPdfPanelRenderTargetFactory(WpfPdfPanel panel)
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

        private WpfPdfPanelRenderTarget GetNewRenderTarget(SKSize canvasSize, SKPoint canvasScale, SKPoint canvasOffset)
        {
            var writeableBitmap = new WriteableBitmap((int)canvasSize.Width, (int)canvasSize.Height, 96.0 * canvasScale.X, 96.0 * canvasScale.Y, PixelFormats.Pbgra32, null);
            return new WpfPdfPanelRenderTarget(writeableBitmap, _panel, canvasSize, canvasScale, canvasOffset);
        }
    }
}