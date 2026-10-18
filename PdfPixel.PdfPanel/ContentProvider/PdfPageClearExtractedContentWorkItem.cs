using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Work item that clears the given types of extracted content of every page, then invokes a callback.
/// </summary>
internal sealed class PdfPageClearExtractedContentWorkItem : IWorkItem
{
    private readonly PdfPageCacheEntry[] _cache;
    private readonly ExtractedContentType _contentTypes;
    private readonly Action<ExtractedContentType> _onCleared;

    /// <summary>
    /// Initializes the work item for the given cache entries and content types.
    /// </summary>
    public PdfPageClearExtractedContentWorkItem(PdfPageCacheEntry[] cache, ExtractedContentType contentTypes, Action<ExtractedContentType> onCleared)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _contentTypes = contentTypes;
        _onCleared = onCleared ?? throw new ArgumentNullException(nameof(onCleared));
    }

    /// <inheritdoc />
    public bool IsSkippable => false;

    /// <inheritdoc />
    public ValueTask ProcessAsync()
    {
        foreach (PdfPageCacheEntry cacheEntry in _cache)
        {
            if ((_contentTypes & ExtractedContentType.Words) != 0)
            {
                cacheEntry.Content.Words.SetContent(null);
            }

            if ((_contentTypes & ExtractedContentType.Thumbnails) != 0)
            {
                cacheEntry.Thumbnail.SetContent(null);
            }
        }

        _onCleared(_contentTypes);

        return default;
    }
}
