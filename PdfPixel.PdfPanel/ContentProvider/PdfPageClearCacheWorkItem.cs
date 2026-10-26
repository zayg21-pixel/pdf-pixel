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
    private readonly IReadOnlyList<PdfPanelPage> _pages;
    private readonly object _documentLocker;
    private readonly CachedContentType _contentTypes;
    private readonly Action<CachedContentType>? _onCleared;

    /// <summary>
    /// Initializes the work item for the given pages and content types.
    /// </summary>
    public PdfPageClearCacheWorkItem(
        IReadOnlyList<PdfPanelPage> pages,
        object documentLocker,
        CachedContentType contentTypes,
        Action<CachedContentType>? onCleared)
    {
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
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
            foreach (PdfPanelPage page in _pages)
            {
                page.CacheEntry.Clear(_contentTypes);
            }
        }

        _onCleared?.Invoke(_contentTypes);

        return default;
    }
}
