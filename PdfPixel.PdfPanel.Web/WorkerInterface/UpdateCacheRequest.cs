using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class UpdateCacheRequest : ContentRequest
{
    public List<int> PagesToStore { get; set; }
}
