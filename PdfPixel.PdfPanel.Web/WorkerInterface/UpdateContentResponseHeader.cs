using PdfPixel.PdfPanel.ContentProvider;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class UpdateContentResponseHeader
{
    public int PageNumber { get; set; }

    public float Scale { get; set; }

    public UpdatedContentType ContentType { get; set; }
}
