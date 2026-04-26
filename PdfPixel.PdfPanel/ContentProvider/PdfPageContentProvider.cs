using PdfPixel.Models;
using PdfPixel.PdfPanel.WorkQueue;
using System;
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
            _cache[i] = new PdfPageCacheEntry(i + 1, PdfDocumentContentExtensions.GetPageInfo(_document, i + 1), PdfDocumentAnnotationExtractor.CreateAnnotationPopups(_document, i + 1));
        }

        _processingQueue = processingQueue;
    }

    public SemaphoreSlim DocumentLocker { get; }

    public Action<PageUpdatedArgs> OnPageUpdated { get; set; }

    public PdfAnnotationPopup[] GetAnnotationPopups(int pageNumber)
    {
        return _cache[pageNumber - 1].Annotations;
    }

    public int GetPagesCount()
    {
        return _cache.Length;
    }

    public PdfContentPictures GetExistingContentPictures(int pageNumber)
    {
        var cacheEntry = _cache[pageNumber - 1];

        return cacheEntry.GetContentPictures();

    }

    public void UpdateContent(UpdateContentRequest request)
    {
        var pagesToStoreSet = new HashSet<int>(request.VisiblePages ?? []);
        List<int> pagesToSend = new List<int>();

        foreach (var cacheEntry in _cache)
        {
            bool sendRequest = false;

            if (!pagesToStoreSet.Contains(cacheEntry.PageNumber))
            {
                cacheEntry.Reset();
            }
            else if (!cacheEntry.PendingRequest)
            {
                sendRequest = true;
            }
            else if (cacheEntry.RenderingParameters != request.RenderingParameters)
            {
                sendRequest = true;
            }

            if (sendRequest)
            {
                pagesToSend.Add(cacheEntry.PageNumber);
            }
        }

        foreach (var pageNumber in pagesToSend)
        {
            var cacheEntry = _cache[pageNumber - 1];
            cacheEntry.InitializeForRendering(request);

            var workItem = new PdfPageUpdateCacheWorkItem(cacheEntry, _document, DocumentLocker, request, OnPageUpdated);
            _processingQueue.Enqueue(workItem);
        }
    }

    public PdfPanelPageInfo GetPageInfo(int pageNumber)
    {
        return _cache[pageNumber - 1].PageInfo;
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