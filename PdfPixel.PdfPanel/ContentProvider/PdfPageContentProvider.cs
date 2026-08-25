using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Collections.Generic;
using System.Linq;

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
    private readonly HashSet<int> _visiblePageNumbers = [];

    /// <summary>
    /// Initializes the provider for <paramref name="document"/>, using <paramref name="processingQueue"/> for background work
    /// and <paramref name="observerFactory"/> to create per-page cancellation observers.
    /// </summary>
    public PdfPageContentProvider(IPdfDocument document, IWorkQueue processingQueue, IPdfExecutionObserverFactory? observerFactory = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _observerFactory = observerFactory ?? new PdfNonYieldingObserverFactory();
        _cache = new PdfPageCacheEntry[document.Pages.Count];

        for (int i = 0; i < document.Pages.Count; i++)
        {
            _cache[i] = new PdfPageCacheEntry(i + 1, PdfDocumentContentExtensions.GetPageInfo(_document, i + 1));
        }

        _processingQueue = processingQueue;
    }

    /// <inheritdoc />
    public object DocumentLocker { get; } = new();

    /// <inheritdoc />
    public Action<PageUpdatedArgs>? OnPageUpdated { get; set; }

    /// <inheritdoc />
    public PdfAnnotationPopup[] GetAnnotationPopups(int pageNumber) => _cache[pageNumber - 1].GetAnnotations(_document, DocumentLocker);

    /// <inheritdoc />
    public int GetPagesCount() => _cache.Length;

    /// <inheritdoc />
    public PdfContentPictures GetExistingContentPictures(int pageNumber)
    {
        PdfPageCacheEntry cacheEntry = _cache[pageNumber - 1];

        return cacheEntry.GetContentPictures();

    }

    /// <inheritdoc />
    public bool NeedsContentUpdate(int pageNumber, PagesDrawingRequest request) => _cache[pageNumber - 1].Content.NeedsPictureUpdate(request);

    /// <inheritdoc />
    public bool NeedsAnnotationUpdate(int pageNumber, PagesDrawingRequest request)
    {
        PdfPageCacheEntry cacheEntry = _cache[pageNumber - 1];

        return cacheEntry.GetAnnotations(_document, DocumentLocker).Length > 0
            && cacheEntry.AnnotationContent.NeedsAnnotationRecordingUpdate(request);
    }

    /// <inheritdoc />
    public void UpdateContent(PagesDrawingRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        HashSet<int> requestedPageNumbers = new(request.VisiblePages.Select(x => x.PageNumber));

        foreach (int pageNumber in _visiblePageNumbers.Except(requestedPageNumbers).ToList())
        {
            PdfPageCacheEntry hiddenEntry = _cache[pageNumber - 1];

            hiddenEntry.Cancel();
            _processingQueue.Enqueue(new PdfPageClearCacheWorkItem(hiddenEntry, DocumentLocker));
        }

        _visiblePageNumbers.Clear();

        foreach (VisiblePageInfo page in request.VisiblePages)
        {
            PdfPageCacheEntry cacheEntry = _cache[page.PageNumber - 1];

            _visiblePageNumbers.Add(page.PageNumber);
            cacheEntry.InitializeForRendering(_observerFactory);
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
