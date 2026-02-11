namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Request to refresh graphics on PDF Panel.
/// Triggers <see cref="IPdfPanelRenderTarget.RenderAsync(SkiaSharp.SKSurface, DrawingRequest)"/>
/// without redrawing all page content.
/// </summary>
internal class RefreshGraphicsDrawingRequest : DrawingRequest
{
}