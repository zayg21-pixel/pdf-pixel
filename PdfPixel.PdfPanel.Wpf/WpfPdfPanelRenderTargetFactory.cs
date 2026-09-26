using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf;

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

        var panelSize = new PdfSize((float)_panel.PanelSize.Width, (float)_panel.PanelSize.Height);
        var hostScale = new PdfPoint((float)_panel.HostScale.X, (float)_panel.HostScale.Y);
        var hostOffset = new PdfPoint((float)_panel.HostOffset.X, (float)_panel.HostOffset.Y);

        if (_previousTarget != null &&
            _previousTarget.PanelSize == panelSize &&
            _previousTarget.HostScale == hostScale &&
            _previousTarget.HostOffset == hostOffset)
        {
            return _previousTarget;
        }

        _previousTarget = GetNewRenderTarget(panelSize, hostScale, hostOffset);

        return _previousTarget;
    }

    private WpfPdfPanelRenderTarget GetNewRenderTarget(PdfSize panelSize, PdfPoint hostScale, PdfPoint hostOffset)
    {
        var writeableBitmap = new WriteableBitmap((int)panelSize.Width, (int)panelSize.Height, 96.0 * hostScale.X, 96.0 * hostScale.Y, PixelFormats.Pbgra32, null);
        return new WpfPdfPanelRenderTarget(writeableBitmap, _panel, panelSize, hostScale, hostOffset);
    }
}
