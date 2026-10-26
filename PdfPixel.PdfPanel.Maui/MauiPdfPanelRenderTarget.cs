using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Maui;

internal sealed class MauiPdfPanelRenderTarget : IPdfPanelRenderTarget
{
    public MauiPdfPanelRenderTarget(MauiPdfPanel panel, float hostScale)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        HostScale = hostScale;
        HostToPanel = PdfMatrix.CreateScale(hostScale, hostScale);
    }

    public MauiPdfPanel Panel { get; }

    public float HostScale { get; }

    /// <inheritdoc />
    public PdfMatrix HostToPanel { get; }

    /// <inheritdoc />
    public void Render(SKSurface surface, PdfPanelFrame frame) => Panel.Present(surface.Snapshot(), frame);
}
