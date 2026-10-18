using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Work item that clears the given types of cached content of pages under the document lock,
/// then invokes a callback when one is given.
/// </summary>
internal sealed class PdfPageClearCacheWorkItem : IWorkItem
{
    private readonly IReadOnlyList<PdfPageCacheEntry> _cacheEntries;
    private readonly object _documentLocker;
    private readonly CachedContentType _contentTypes;
    private readonly Action<CachedContentType>? _onCleared;

    /// <summary>
    /// Initializes the work item for the given cache entries and content types.
    /// </summary>
    public PdfPageClearCacheWorkItem(
        IReadOnlyList<PdfPageCacheEntry> cacheEntries,
        object documentLocker,
        CachedContentType contentTypes,
        Action<CachedContentType>? onCleared)
    {
        _cacheEntries = cacheEntries ?? throw new ArgumentNullException(nameof(cacheEntries));
        _documentLocker = documentLocker ?? throw new ArgumentNullException(nameof(documentLocker));
        _contentTypes = contentTypes;
        _onCleared = onCleared;
    }

    /// <inheritdoc />
    public bool IsSkippable => false;

    /// <inheritdoc />
    public ValueTask ProcessAsync()
    {
        lock (_documentLocker)
        {
            foreach (PdfPageCacheEntry cacheEntry in _cacheEntries)
            {
                cacheEntry.Clear(_contentTypes);
            }
        }

        _onCleared?.Invoke(_contentTypes);

        return default;
    }
}
