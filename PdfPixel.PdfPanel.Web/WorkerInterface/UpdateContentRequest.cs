namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// Worker request to update visible page content.
/// </summary>
public class UpdateContentRequest
{
    public WebDrawingRequest DrawingRequest { get; set; }
}
