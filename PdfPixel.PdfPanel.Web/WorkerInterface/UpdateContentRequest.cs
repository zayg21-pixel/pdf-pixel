using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class UpdateContentRequest
{
    public List<int> VisiblePages { get; set; }

    public float Scale { get; set; }
}
