using PdfPixel.PdfPanel.WorkQueue;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Work item that clears a page's cached content under the document lock.
/// Enqueued for pages that scroll out of view.
/// </summary>
public sealed class PdfPageClearCacheWorkItem : IWorkItem
{
    private readonly PdfPageCacheEntry _cacheEntry;
    private readonly object _documentLocker;

    /// <summary>
    /// Initializes the work item for the given cache entry.
    /// </summary>
    public PdfPageClearCacheWorkItem(PdfPageCacheEntry cacheEntry, object documentLocker)
    {
        _cacheEntry = cacheEntry;
        _documentLocker = documentLocker;
    }

    /// <inheritdoc />
    public bool IsSkippable => false;

    /// <inheritdoc />
    public ValueTask ProcessAsync()
    {
        lock (_documentLocker)
        {
            _cacheEntry.Clear();
        }

        return default;
    }
}
