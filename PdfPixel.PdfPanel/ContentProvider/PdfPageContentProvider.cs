using PdfPixel.Models;
using PdfPixel.PdfPanel.WorkQueue;
using SkiaSharp;
using System.Collections.Generic;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

public sealed class PdfPageContentProvider : IPdfPageContentProvider
{
    private readonly PdfDocument _document;
    private readonly IWorkQueue<PdfPageUpdateCacheWorkItem> _processingQueue;
    private readonly PdfPageCacheEntry[] _cache;

    public PdfPageContentProvider(PdfDocument document, IWorkQueue<PdfPageUpdateCacheWorkItem> processingQueue)
    {
        DocumentLocker = new SemaphoreSlim(1, 1);
        _document = document;
        _cache = new PdfPageCacheEntry[document.Pages.Count];

        for (int i = 0; i < document.Pages.Count; i++)
        {
            _cache[i] = new PdfPageCacheEntry(i + 1, GetPageInfo(i + 1));
        }

        _processingQueue = processingQueue;
    }

    public SemaphoreSlim DocumentLocker { get; }

    public int GetPagesCount()
    {
        return _cache.Length;
    }

    public void RefreshCache(IEnumerable<int> pagesToStore, CancellationTokenSource cancellationTokenSource)
    {
        var pagesToStoreSet = new HashSet<int>(pagesToStore ?? []);

        foreach (var cacheEntry in _cache)
        {
            if (!pagesToStoreSet.Contains(cacheEntry.PageNumber))
            {
                cacheEntry.Clear();
            }
        }
    }

    public ContentLocker<SKPicture> GetExistingContent(int pageNumber)
    {
        var cacheEntry = _cache[pageNumber - 1];
        return cacheEntry.Content.ContentPicture;
    }

    public ContentLocker<SKPicture> GetExistingAnnotationContent(int pageNumber)
    {
        var cacheEntry = _cache[pageNumber - 1];
        return cacheEntry.AnnotationContent.ContentPicture;
    }

    public void UpdateContent(ContentProviderRequest request)
    {
        var cacheEntry = _cache[request.PageNumber - 1];
        var workItem = new PdfPageUpdateCacheWorkItem(cacheEntry, _document, DocumentLocker, request);
        _processingQueue.Enqueue(workItem);
    }

    public PdfPanelPageInfo GetPageInfo(int pageNumber)
    {
        DocumentLocker.Wait();

        var result = PdfDocumentContentExtensions.GetPageInfo(_document, pageNumber);

        DocumentLocker.Release();
        return result;
    }

    public void Dispose()
    {
        _processingQueue.Dispose();

        foreach (var cacheEntry in _cache)
        {
            cacheEntry.Dispose();
        }

        DocumentLocker.Dispose();
    }
}