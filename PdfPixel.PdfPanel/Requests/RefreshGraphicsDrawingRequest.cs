namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Request to refresh graphics on PDF Panel.
/// Triggers <see cref="PdfPanelPageCollection.OnAfterDraw"/> without redrawing all page content.
/// </summary>
public class RefreshGraphicsDrawingRequest : DrawingRequest
{
}