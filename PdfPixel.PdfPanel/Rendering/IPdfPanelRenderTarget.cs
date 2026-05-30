using PdfPixel.PdfPanel.Requests;
using SkiaSharp;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Presents a rendered <see cref="SKSurface"/> to the screen.
/// Implementations copy pixels to the platform-specific display surface (e.g. WriteableBitmap, D3DImage).
/// </summary>
public interface IPdfPanelRenderTarget
{
    /// <summary>
    /// Presents the contents of <paramref name="surface"/> to the display.
    /// Called on the UI thread after each render pass.
    /// </summary>
    void Render(SKSurface surface, DrawingRequest request);
}

