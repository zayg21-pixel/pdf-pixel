using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Represents a rendering target for a PDF panel, capable of rendering content onto a <see cref="SKSurface"/>.
/// </summary>
public interface IPdfPanelRenderTarget
{
    /// <summary>
    /// Asynchronously renders content onto the specified <see cref="SKSurface"/>.
    /// </summary>
    /// <param name="surface">The surface to render onto.</param>
    /// <returns>A task representing the asynchronous rendering operation.</returns>
    Task RenderAsync(SKSurface surface, DrawingRequest request);
}
