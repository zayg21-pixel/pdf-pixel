using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class RefreshCacheRequest : ContentRequest
{
    public List<int> PagesToStore { get; set; }
}
