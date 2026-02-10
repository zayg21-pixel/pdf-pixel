namespace PdfPixel.Canvas.Requests;

/// <summary>
/// Request to refresh graphics on <see cref="SkiaPdfPanel"/>.
/// Triggers <see cref="PdfViewerPageCollection.OnAfterDraw"/> without redrawing all page content.
/// </summary>
internal class RefreshGraphicsDrawingRequest : DrawingRequest
{
}