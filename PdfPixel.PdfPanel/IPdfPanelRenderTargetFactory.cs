namespace PdfPixel.PdfPanel;

/// <summary>
/// Defines a factory for creating <see cref="IPdfPanelRenderTarget"/> instances for PDF panel rendering.
/// </summary>
public interface IPdfPanelRenderTargetFactory
{
    /// <summary>
    /// Creates a new <see cref="IPdfPanelRenderTarget"/> for the specified panel context.
    /// </summary>
    /// <param name="context">The panel context used to configure the render target.</param>
    /// <returns>A new <see cref="IPdfPanelRenderTarget"/> instance.</returns>
    IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context);
}
