using PdfPixel.Geometry;
using SkiaSharp;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Presents a rendered <see cref="SKSurface"/> to the screen.
/// Implementations copy pixels to the platform-specific display surface (e.g. WriteableBitmap, D3DImage).
/// </summary>
public interface IPdfPanelRenderTarget
{
    /// <summary>
    /// Matrix that maps host coordinates to panel pixels.
    /// </summary>
    PdfMatrix HostToPanel { get; }

    /// <summary>
    /// Presents the contents of <paramref name="surface"/> to the display.
    /// Called on the UI thread after each render pass.
    /// </summary>
    void Render(SKSurface surface, PdfPanelFrame frame);
}

