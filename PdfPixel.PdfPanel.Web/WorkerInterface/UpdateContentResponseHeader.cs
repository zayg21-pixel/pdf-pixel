using PdfPixel.PdfPanel.ContentProvider;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class UpdateContentResponseHeader
{
    public bool IsComplete { get; set; }

    public int PageNumber { get; set; }

    public UpdatedContentType ContentType { get; set; }

    public bool IsPartialContent { get; set; }

    public WebDrawingRequest DrawingRequest { get; set; }

    public WebRect RegionOfInterest { get; set; }

    public List<WebTextCharacter> Characters { get; set; }
}
