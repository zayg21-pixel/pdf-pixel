using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class RefreshCacheRequest
{
    public List<int> PagesToStore { get; set; }
}
