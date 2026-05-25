using PdfPixel.Models;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.ContentProvider;

public sealed class PdfPageContentProvider : IPdfPageContentProvider
{
    private readonly IPdfDocument _document;
    private readonly IWorkQueue _processingQueue;
    private readonly PdfPageCacheEntry[] _cache;

    public PdfPageContentProvider(IPdfDocument document, IWorkQueue processingQueue)
    {
        _document = document;
        _cache = new PdfPageCacheEntry[document.Pages.Count];

        for (int i = 0; i < document.Pages.Count; i++)
        {
            _cache[i] = new PdfPageCacheEntry(i + 1, PdfDocumentContentExtensions.GetPageInfo(_document, i + 1), PdfDocumentAnnotationExtractor.CreateAnnotationPopups(_document, i + 1));
        }

        _processingQueue = processingQueue;
    }

    public object DocumentLocker { get; } = new object();

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
        var visiblePageNumbers = new HashSet<int>(request.VisiblePages ?? []);

        foreach (var cacheEntry in _cache)
        {
            if (!visiblePageNumbers.Contains(cacheEntry.PageNumber))
            {
                cacheEntry.Cancel();
                _processingQueue.Enqueue(new PdfPageClearCacheWorkItem(cacheEntry, DocumentLocker));
                continue;
            }

            bool needsUpdate = !cacheEntry.PendingRequest || cacheEntry.RenderingParameters != request.RenderingParameters;
            if (!needsUpdate) continue;

            cacheEntry.InitializeForRendering(request);
            _processingQueue.Enqueue(new PdfPageUpdateCacheWorkItem(cacheEntry, _document, DocumentLocker, request, OnPageUpdated));
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
    }
}