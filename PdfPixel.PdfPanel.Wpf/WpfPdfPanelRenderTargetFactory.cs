using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf;

/// <summary>
/// Render target factory that presents CPU-rendered surfaces through a <see cref="WriteableBitmap"/>.
/// </summary>
public class WpfPdfPanelRenderTargetFactory : IPdfPanelRenderTargetFactory
{
    private readonly WpfPdfPanel _panel;
    private WpfPdfPanelRenderTarget? _previousTarget;

    /// <summary>
    /// Initializes a new <see cref="WpfPdfPanelRenderTargetFactory"/> for the specified panel.
    /// </summary>
    /// <param name="panel">The WPF panel that owns the <see cref="DrawingVisual"/>.</param>
    public WpfPdfPanelRenderTargetFactory(WpfPdfPanel panel) => _panel = panel;

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        PdfSize panelSize = new((float)_panel.PanelSize.Width, (float)_panel.PanelSize.Height);
        PdfPoint hostScale = new((float)_panel.HostScale.X, (float)_panel.HostScale.Y);
        PdfPoint hostOffset = new((float)_panel.HostOffset.X, (float)_panel.HostOffset.Y);

        if (_previousTarget != null
            && _previousTarget.PanelSize == panelSize
            && _previousTarget.HostScale == hostScale
            && _previousTarget.HostOffset == hostOffset)
        {
            return _previousTarget;
        }

        _previousTarget = GetNewRenderTarget(panelSize, hostScale, hostOffset);

        return _previousTarget;
    }

    private WpfPdfPanelRenderTarget GetNewRenderTarget(in PdfSize panelSize, in PdfPoint hostScale, in PdfPoint hostOffset)
    {
        WriteableBitmap writeableBitmap = new((int)panelSize.Width, (int)panelSize.Height, 96.0 * hostScale.X, 96.0 * hostScale.Y, PixelFormats.Pbgra32, null);
        return new WpfPdfPanelRenderTarget(writeableBitmap, _panel, panelSize, hostScale, hostOffset);
    }
}
