using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Represents a rendering target for a PDF panel, capable of rendering content onto a <see cref="SKSurface"/>.
/// </summary>
public interface IPdfPanelRenderTarget
{
    /// <summary>
    /// Renders content onto the specified <see cref="SKSurface"/>.
    /// </summary>
    /// <param name="surface">The surface to render onto.</param>
    /// <param name="request">The drawing request.</param>
    /// <param name="token">Token to cancel render request.</param>
    void Render(SKSurface surface, DrawingRequest request, CancellationToken token);
}
