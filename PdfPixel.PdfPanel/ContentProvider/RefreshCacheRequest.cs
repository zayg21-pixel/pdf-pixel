using System.Collections.Generic;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

public class RefreshCacheRequest
{
    public List<int> VisiblePages { get; set; }

    public CancellationTokenSource CancellationTokenSource { get; set; }
}
