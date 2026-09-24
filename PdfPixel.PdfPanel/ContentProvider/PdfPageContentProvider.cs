using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.TextExtraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
    private readonly PdfTextBlockFlattener _textBlockFlattener = new();
    private volatile bool _extractText;
    private volatile int _textExtractionStartPageNumber = 1;
    private int _textExtractionPending;

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
    public event EventHandler<PageTextExtractedEventArgs>? PageTextExtracted;

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
    public PdfCharacter[]? GetCharacters(int pageNumber) => _cache[pageNumber - 1].Content.Characters;

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
            _processingQueue.Enqueue(new PdfPageUpdateCacheWorkItem(cacheEntry, _document, DocumentLocker, _textBlockFlattener, request, OnPageUpdated, OnPageTextExtracted));
        }

        if (request.VisiblePages.Length > 0)
        {
            _textExtractionStartPageNumber = request.VisiblePages[0].PageNumber;
        }
    }

    /// <inheritdoc />
    public void UpdateTextExtraction(bool extractText)
    {
        _extractText = extractText;
        EnqueueNextTextExtraction();
    }

    /// <inheritdoc />
    public PdfPanelPageInfo GetPageInfo(int pageNumber) => _cache[pageNumber - 1].PageInfo;

    private void EnqueueNextTextExtraction()
    {
        if (!_extractText || Interlocked.CompareExchange(ref _textExtractionPending, 1, 0) != 0)
        {
            return;
        }

        PdfPageCacheEntry? nextEntry = FindNextEntryWithoutCharacters();

        if (nextEntry == null)
        {
            Volatile.Write(ref _textExtractionPending, 0);
            return;
        }

        IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(nextEntry.PageNumber);
        _processingQueue.Enqueue(new PdfPageExtractTextWorkItem(nextEntry, _document, DocumentLocker, _textBlockFlattener, observer, OnTextExtractionCompleted));
    }

    private void OnPageTextExtracted(int pageNumber) => PageTextExtracted?.Invoke(this, new PageTextExtractedEventArgs(pageNumber));

    private void OnTextExtractionCompleted(int pageNumber)
    {
        if (_cache[pageNumber - 1].Content.Characters != null)
        {
            OnPageTextExtracted(pageNumber);
        }

        Volatile.Write(ref _textExtractionPending, 0);
        EnqueueNextTextExtraction();
    }

    private PdfPageCacheEntry? FindNextEntryWithoutCharacters()
    {
        int startIndex = _textExtractionStartPageNumber - 1;

        for (int offset = 0; offset < _cache.Length; offset++)
        {
            PdfPageCacheEntry cacheEntry = _cache[(startIndex + offset) % _cache.Length];

            if (cacheEntry.Content.Characters == null)
            {
                return cacheEntry;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _extractText = false;
        _processingQueue.Dispose();

        foreach (PdfPageCacheEntry cacheEntry in _cache)
        {
            cacheEntry.Dispose();
        }
    }
}
