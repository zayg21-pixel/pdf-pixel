using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.TextExtraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Provides decoded page content and annotation pictures for rendering.
/// Decodes page content and annotations on a background worker thread and notifies the UI via <see cref="OnPageUpdated"/>.
/// </summary>
internal sealed class PdfPageContentProvider : IDisposable
{
    private readonly IPdfDocument _document;
    private readonly IWorkQueue _processingQueue;
    private readonly IPdfExecutionObserverFactory _observerFactory;
    private readonly PdfPageCacheEntry[] _cache;
    private readonly HashSet<int> _visiblePageNumbers = [];
    private readonly PdfTextBlockFlattener _textBlockFlattener = new();
    private readonly PdfTextChunker _textChunker = new();
    private volatile bool _extractText;
    private volatile int _textExtractionStartPageNumber = 1;
    private int _textExtractionPending;

    /// <summary>
    /// Initializes the provider for <paramref name="document"/>, using <paramref name="processingQueue"/> for background work
    /// and <paramref name="observerFactory"/> to create per-page cancellation observers.
    /// </summary>
    public PdfPageContentProvider(IPdfDocument document, IWorkQueue processingQueue, IPdfExecutionObserverFactory observerFactory)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _observerFactory = observerFactory ?? throw new ArgumentNullException(nameof(observerFactory));
        _cache = new PdfPageCacheEntry[document.Pages.Count];

        for (int i = 0; i < document.Pages.Count; i++)
        {
            _cache[i] = new PdfPageCacheEntry(i + 1, PdfDocumentContentExtensions.GetPageInfo(_document, i + 1));
        }

        _processingQueue = processingQueue;
    }

    /// <summary>
    /// Raised on the work queue thread when a page's characters have been extracted, by text extraction or by rendering.
    /// </summary>
    public event EventHandler<PageTextExtractedEventArgs>? PageTextExtracted;

    /// <summary>
    /// Synchronisation object used to serialise access to the underlying PDF document.
    /// </summary>
    public object DocumentLocker { get; } = new();

    /// <summary>
    /// Called on the UI thread whenever a page's content or annotations have been decoded and are ready to render.
    /// </summary>
    public Action<PageUpdatedArgs>? OnPageUpdated { get; set; }

    /// <summary>
    /// Returns the annotation popups for the specified 1-based page number.
    /// </summary>
    public PdfAnnotationPopup[] GetAnnotationPopups(int pageNumber) => _cache[pageNumber - 1].GetAnnotations(_document, DocumentLocker);

    /// <summary>
    /// Returns the total number of pages in the document.
    /// </summary>
    public int GetPagesCount() => _cache.Length;

    /// <summary>
    /// Returns the currently cached <see cref="PdfContentPictures"/> for the specified 1-based page number.
    /// Returns empty pictures if the page has not been decoded yet.
    /// </summary>
    public PdfContentPictures GetExistingContentPictures(int pageNumber)
    {
        PdfPageCacheEntry cacheEntry = _cache[pageNumber - 1];

        return cacheEntry.GetContentPictures();

    }

    /// <summary>
    /// Returns the extracted words of the specified 1-based page number in reading order,
    /// or <see langword="null"/> if they have not been extracted yet.
    /// </summary>
    public PdfWord[]? GetWords(int pageNumber) => _cache[pageNumber - 1].Content.Words;

    /// <summary>
    /// Returns <see langword="true"/> when <see cref="UpdateContent"/> would regenerate the content
    /// picture of the specified 1-based page number for <paramref name="request"/>.
    /// </summary>
    public bool NeedsContentUpdate(int pageNumber, PagesDrawingRequest request) => _cache[pageNumber - 1].Content.NeedsPictureUpdate(request);

    /// <summary>
    /// Returns <see langword="true"/> when <see cref="UpdateContent"/> would regenerate the annotation
    /// recording of the specified 1-based page number for <paramref name="request"/>.
    /// </summary>
    public bool NeedsAnnotationUpdate(int pageNumber, PagesDrawingRequest request)
    {
        PdfPageCacheEntry cacheEntry = _cache[pageNumber - 1];

        return cacheEntry.GetAnnotations(_document, DocumentLocker).Length > 0
            && cacheEntry.AnnotationContent.NeedsAnnotationRecordingUpdate(request);
    }

    /// <summary>
    /// Starts or updates background decoding for the pages described by <paramref name="request"/>.
    /// Pages no longer visible are cancelled and their cache cleared.
    /// </summary>
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
            _processingQueue.Enqueue(new PdfPageUpdateCacheWorkItem(cacheEntry, _document, DocumentLocker, _textBlockFlattener, _textChunker, request, OnPageUpdated, OnPageTextExtracted));
        }

        if (request.VisiblePages.Length > 0)
        {
            _textExtractionStartPageNumber = request.VisiblePages[0].PageNumber;
        }
    }

    /// <summary>
    /// Starts or stops extracting the characters of every page that has none yet, one page at a time.
    /// </summary>
    public void UpdateTextExtraction(bool extractText)
    {
        _extractText = extractText;
        EnqueueNextTextExtraction();
    }

    /// <summary>
    /// Returns the <see cref="PdfPanelPageInfo"/> for the specified 1-based page number.
    /// </summary>
    public PdfPanelPageInfo GetPageInfo(int pageNumber) => _cache[pageNumber - 1].PageInfo;

    private void EnqueueNextTextExtraction()
    {
        if (!_extractText || Interlocked.CompareExchange(ref _textExtractionPending, 1, 0) != 0)
        {
            return;
        }

        PdfPageCacheEntry? nextEntry = FindNextEntryWithoutWords();

        if (nextEntry == null)
        {
            Volatile.Write(ref _textExtractionPending, 0);
            return;
        }

        IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(nextEntry.PageNumber);
        _processingQueue.Enqueue(new PdfPageExtractTextWorkItem(nextEntry, _document, DocumentLocker, _textBlockFlattener, _textChunker, observer, OnTextExtractionCompleted));
    }

    private void OnPageTextExtracted(int pageNumber) => PageTextExtracted?.Invoke(this, new PageTextExtractedEventArgs(pageNumber));

    private void OnTextExtractionCompleted(int pageNumber)
    {
        if (_cache[pageNumber - 1].Content.Words != null)
        {
            OnPageTextExtracted(pageNumber);
        }

        Volatile.Write(ref _textExtractionPending, 0);
        EnqueueNextTextExtraction();
    }

    private PdfPageCacheEntry? FindNextEntryWithoutWords()
    {
        int startIndex = _textExtractionStartPageNumber - 1;

        for (int offset = 0; offset < _cache.Length; offset++)
        {
            PdfPageCacheEntry cacheEntry = _cache[(startIndex + offset) % _cache.Length];

            if (cacheEntry.Content.Words == null)
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
