using PdfPixel.PdfPanel.WorkQueue;

namespace PdfPixel.PdfPanel.ContentProvider;

public sealed class PdfPageClearCacheWorkItem : IWorkItem
{
    private readonly PdfPageCacheEntry _cacheEntry;
    private readonly object _documentLocker;

    public PdfPageClearCacheWorkItem(PdfPageCacheEntry cacheEntry, object documentLocker)
    {
        _cacheEntry = cacheEntry;
        _documentLocker = documentLocker;
    }

    public bool IsSkippable => false;

    public void Process()
    {
        lock (_documentLocker)
        {
            _cacheEntry.Clear();
        }
    }
}
