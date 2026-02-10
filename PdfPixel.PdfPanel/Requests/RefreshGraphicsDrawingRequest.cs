namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Request to refresh graphics on <see cref="SkiaPdfPanel"/>.
/// Triggers <see cref="PdfPanelPageCollection.OnAfterDraw"/> without redrawing all page content.
/// </summary>
internal class RefreshGraphicsDrawingRequest : DrawingRequest
{
}