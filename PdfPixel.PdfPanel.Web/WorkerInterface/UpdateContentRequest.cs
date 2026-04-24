namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class UpdateContentRequest : ContentRequest
{
    public int PageNumber { get; set; }

    public int Scale { get; set; }
}
