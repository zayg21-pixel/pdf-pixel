using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Default <see cref="IPdfPageContentProvider"/> implementation.
/// Decodes page content and annotations on a background worker thread and notifies the UI via <see cref="OnPageUpdated"/>.
/// </summary>
public sealed class PdfPageContentProvider : IPdfPageContentProvider
{
    private readonly IPdfDocument _document;
    private readonly IWorkQueue _processingQueue;
    private readonly IPdfExecutionObserverFactory _observerFactory;
    private readonly PdfPageCacheEntry[] _cache;

    /// <summary>
    /// Initializes the provider for <paramref name="document"/>, using <paramref name="processingQueue"/> for background work
    /// and <paramref name="observerFactory"/> to create per-page cancellation observers.
    /// </summary>
    public PdfPageContentProvider(IPdfDocument document, IWorkQueue processingQueue, IPdfExecutionObserverFactory? observerFactory = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _observerFactory = observerFactory ?? new PdfCancellationSourceObserverFactory();
        _cache = new PdfPageCacheEntry[document.Pages.Count];

        for (int i = 0; i < document.Pages.Count; i++)
        {
            _cache[i] = new PdfPageCacheEntry(i + 1, PdfDocumentContentExtensions.GetPageInfo(_document, i + 1), _document.CreateAnnotationPopups(i + 1));
        }

        _processingQueue = processingQueue;
    }

    /// <inheritdoc />
    public object DocumentLocker { get; } = new();

    /// <inheritdoc />
    public Action<PageUpdatedArgs>? OnPageUpdated { get; set; }

    /// <inheritdoc />
    public PdfAnnotationPopup[]? GetAnnotationPopups(int pageNumber) => _cache[pageNumber - 1].Annotations;

    /// <inheritdoc />
    public int GetPagesCount() => _cache.Length;

    /// <inheritdoc />
    public PdfContentPictures GetExistingContentPictures(int pageNumber)
    {
        PdfPageCacheEntry cacheEntry = _cache[pageNumber - 1];

        return cacheEntry.GetContentPictures();

    }

    /// <inheritdoc />
    public void UpdateContent(UpdateContentRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        HashSet<int> visiblePageNumbers = new(request.VisiblePages ?? Array.Empty<int>());

        foreach (PdfPageCacheEntry cacheEntry in _cache)
        {
            if (!visiblePageNumbers.Contains(cacheEntry.PageNumber))
            {
                cacheEntry.Cancel();
                _processingQueue.Enqueue(new PdfPageClearCacheWorkItem(cacheEntry, DocumentLocker));
                continue;
            }

            if (!cacheEntry.NeedsUpdate(request))
            {
                continue;
            }

            IPdfCancellableExecutionObserver parseObserver = _observerFactory.CreateParseObserver(cacheEntry.PageNumber);
            IPdfCancellableExecutionObserver contentObserver = _observerFactory.CreateContentObserver(cacheEntry.PageNumber);
            cacheEntry.InitializeForRendering(request, parseObserver, contentObserver);
            _processingQueue.Enqueue(new PdfPageUpdateCacheWorkItem(cacheEntry, _document, DocumentLocker, request, OnPageUpdated));
        }
    }

    /// <inheritdoc />
    public PdfPanelPageInfo GetPageInfo(int pageNumber) => _cache[pageNumber - 1].PageInfo;

    /// <inheritdoc />
    public void Dispose()
    {
        _processingQueue.Dispose();

        foreach (PdfPageCacheEntry cacheEntry in _cache)
        {
            cacheEntry.Dispose();
        }
    }
}
