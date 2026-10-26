using PdfPixel.PdfPanel.Rendering;
using System;

namespace PdfPixel.PdfPanel.Maui;

/// <summary>
/// Render target factory that presents CPU-rendered surfaces through the canvas of a <see cref="MauiPdfPanel"/>.
/// </summary>
public class MauiPdfPanelRenderTargetFactory : IPdfPanelRenderTargetFactory
{
    private readonly MauiPdfPanel _panel;
    private MauiPdfPanelRenderTarget? _previousTarget;

    /// <summary>
    /// Initializes a new <see cref="MauiPdfPanelRenderTargetFactory"/> for the specified panel.
    /// </summary>
    /// <param name="panel">The MAUI panel that draws the presented surfaces.</param>
    public MauiPdfPanelRenderTargetFactory(MauiPdfPanel panel) => _panel = panel;

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (_previousTarget != null && _previousTarget.HostScale == _panel.HostScale)
        {
            return _previousTarget;
        }

        _previousTarget = new MauiPdfPanelRenderTarget(_panel, _panel.HostScale);

        return _previousTarget;
    }
}
