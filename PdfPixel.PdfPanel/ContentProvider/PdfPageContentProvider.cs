using PdfPixel.Commands.Model;
using PdfPixel.Content;
using PdfPixel.Content.Model;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Content;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Thumbnails;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Provides decoded page content and annotation pictures for rendering.
/// Decodes page content and annotations through its work queue and notifies the UI via <see cref="OnPageUpdated"/>.
/// </summary>
internal sealed class PdfPageContentProvider : IDisposable
{
    private static readonly PdfContentKind[] ExtractedContentKinds = [PdfContentKind.Path, PdfContentKind.Image, PdfContentKind.Shading, PdfContentKind.Form];

    private readonly IPdfDocument _document;
    private readonly IWorkQueue _processingQueue;
    private readonly IPdfExecutionObserverFactory _observerFactory;
    private readonly PdfPageCacheEntry[] _cache;
    private readonly HashSet<int> _visiblePageNumbers = [];
    private readonly HashSet<int> _lookAheadPageNumbers = [];
    private readonly List<int> _contentCachePageNumbers = [];
    private readonly object _thumbnailRequestLocker = new();
    private readonly Dictionary<int, IPdfCancellableExecutionObserver?> _thumbnailRequests = [];
    private PagesDrawingRequest? _lastLookAheadRequest;
    private readonly PdfTextExtractor _textExtractor = new();
    private readonly PdfTextExtractionProperties _textExtractionProperties = new();
    private readonly PdfContentExtractor _contentExtractor = new();
    private readonly PdfContentExtractionParameters _contentExtractionParameters = new() { ContentKinds = ExtractedContentKinds };
    private volatile IReadOnlyDictionary<PdfReference, bool>? _optionalContentStates;
    private volatile bool _extractText;
    private volatile int _textExtractionStartPageNumber = 1;
    private int _textExtractionPending;
    private IPdfCancellableExecutionObserver? _textExtractionObserver;
    private IPdfCancellableExecutionObserver? _thumbnailChainObserver;
    private volatile bool _usePageThumbnails;
    private volatile bool _renderPageThumbnails;
    private PdfIntegerSize _thumbnailMaximumSize;
    private volatile int _maximumThumbnailCount;
    private volatile PdfPanelThumbnailPreloadMode _thumbnailPreloadMode;
    private volatile int[] _visiblePageNumbersInOrder = [];
    private volatile bool _produceThumbnails;
    private volatile int _thumbnailCenterPageNumber = 1;
    private int _thumbnailPending;

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
    /// Raised from a work item when a page's characters have been extracted, by text extraction or by rendering.
    /// </summary>
    public event EventHandler<PageTextExtractedEventArgs>? PageTextExtracted;

    /// <summary>
    /// Raised from a work item when the thumbnail request made with <see cref="RequestThumbnail"/> finished.
    /// </summary>
    public event EventHandler<PageThumbnailRenderedEventArgs>? PageThumbnailRendered;

    /// <summary>
    /// Raised from a work item when extracted content of every page was cleared.
    /// </summary>
    public event EventHandler<ExtractedContentClearedEventArgs>? ExtractedContentCleared;

    /// <summary>
    /// Synchronisation object used to serialise access to the underlying PDF document.
    /// </summary>
    public object DocumentLocker { get; } = new();

    /// <summary>
    /// Document the pages are decoded from.
    /// </summary>
    public IPdfDocument Document => _document;

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
    public PdfWord[]? GetWords(int pageNumber)
    {
        using LockedContent<PdfWord[]> lockedWords = _cache[pageNumber - 1].Content.Words.GetContent();

        return lockedWords.Content;
    }

    /// <summary>
    /// Returns the thumbnail of the specified 1-based page number as a PNG file,
    /// or <see langword="null"/> if it has not been produced yet.
    /// </summary>
    public byte[]? GetThumbnail(int pageNumber)
    {
        using LockedContent<byte[]> lockedThumbnail = _cache[pageNumber - 1].Thumbnail.GetContent();

        return lockedThumbnail.Content;
    }

    /// <summary>
    /// Returns the extracted content items of the specified 1-based page number in drawing order,
    /// or <see langword="null"/> if they have not been extracted yet.
    /// </summary>
    public PdfPanelContentItem[]? GetContent(int pageNumber)
    {
        using LockedContent<PdfPanelContentItem[]> lockedContentItems = _cache[pageNumber - 1].Content.ContentItems.GetContent();

        return lockedContentItems.Content;
    }

    /// <summary>
    /// Returns the information about the rendered content of the specified 1-based page number,
    /// or <see langword="null"/> if it has not been rendered yet.
    /// </summary>
    public PdfPanelRenderedLayerInfo? GetRenderedContentInfo(int pageNumber) => _cache[pageNumber - 1].Content.GetRenderedLayerInfo();

    /// <summary>
    /// Returns the information about the rendered annotations of the specified 1-based page number,
    /// or <see langword="null"/> if they have not been rendered yet.
    /// </summary>
    public PdfPanelRenderedLayerInfo? GetRenderedAnnotationsInfo(int pageNumber) => _cache[pageNumber - 1].AnnotationContent.GetRenderedLayerInfo();

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
    /// Starts or updates decoding for the pages described by <paramref name="request"/>.
    /// A visible request cancels the decoding of every page that is no longer visible and all background work, decodes its pages first
    /// and enqueues the remaining background work again after them: the pages of the last look-ahead request, the requested thumbnails,
    /// then the thumbnail and text extraction chains. A look-ahead request cancels the look-ahead pages it no longer holds.
    /// The decoded content of at most <see cref="PagesDrawingRequest.ContentCacheSize"/> pages besides the visible ones is kept,
    /// clearing the least recently requested ones beyond that.
    /// </summary>
    public void UpdateContent(PagesDrawingRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        HashSet<int> requestedPageNumbers = new(request.Pages.Select(page => page.PageNumber));

        if (request.IsLookAhead)
        {
            CancelPages(_lookAheadPageNumbers.Except(requestedPageNumbers));
            _lookAheadPageNumbers.Clear();
            _lookAheadPageNumbers.UnionWith(requestedPageNumbers);
            _lastLookAheadRequest = request;
            EnqueueContentUpdates(request);
            EvictContentCache(request.ContentCacheSize);
            return;
        }

        CancelBackgroundWork();
        CancelPages(_lookAheadPageNumbers.Union(_visiblePageNumbers).Except(requestedPageNumbers));
        _visiblePageNumbers.Clear();
        _visiblePageNumbers.UnionWith(requestedPageNumbers);
        _visiblePageNumbersInOrder = request.Pages.Select(page => page.PageNumber).ToArray();
        _lookAheadPageNumbers.ExceptWith(requestedPageNumbers);

        EnqueueContentUpdates(request);

        if (_lastLookAheadRequest != null)
        {
            foreach (int pageNumber in _lookAheadPageNumbers)
            {
                EnqueueContentUpdate(_cache[pageNumber - 1], _lastLookAheadRequest);
            }
        }

        RequeueThumbnailRequests();
        EvictContentCache(request.ContentCacheSize);

        if (request.Pages.Length > 0)
        {
            _textExtractionStartPageNumber = request.Pages[0].PageNumber;
            _thumbnailCenterPageNumber = request.Pages[0].PageNumber;
            _produceThumbnails = true;
            EnqueueNextThumbnail();
            EnqueueNextTextExtraction();
        }
    }

    /// <summary>
    /// Sets the optional content states that words and thumbnails are produced with.
    /// Clears all extracted content when the states changed.
    /// </summary>
    public void SetOptionalContentStates(IReadOnlyDictionary<PdfReference, bool>? optionalContentStates)
    {
        if (PagesDrawingRequest.AreOptionalContentStatesEqual(_optionalContentStates, optionalContentStates))
        {
            return;
        }

        _optionalContentStates = optionalContentStates;
        ClearExtractedContent();
    }

    /// <summary>
    /// Sets how thumbnails are produced: from the page's thumbnail image when <paramref name="usePageThumbnails"/> is set and the page has one,
    /// otherwise by rendering the page when <paramref name="renderPages"/> is set, fitted within <paramref name="renderMaximumSize"/>,
    /// keeping at most <paramref name="maximumCount"/> thumbnails nearest to the first visible page,
    /// and producing unrequested thumbnails for the pages selected by <paramref name="preloadMode"/>.
    /// Clears the thumbnails when a setting other than <paramref name="maximumCount"/> or <paramref name="preloadMode"/> changed.
    /// </summary>
    public void SetThumbnailSettings(
        bool usePageThumbnails,
        bool renderPages,
        in PdfIntegerSize renderMaximumSize,
        int maximumCount,
        PdfPanelThumbnailPreloadMode preloadMode)
    {
        bool thumbnailsChanged = usePageThumbnails != _usePageThumbnails
            || renderPages != _renderPageThumbnails
            || renderMaximumSize != _thumbnailMaximumSize;

        _usePageThumbnails = usePageThumbnails;
        _renderPageThumbnails = renderPages;
        _thumbnailMaximumSize = renderMaximumSize;
        _maximumThumbnailCount = maximumCount;
        _thumbnailPreloadMode = preloadMode;

        if (thumbnailsChanged)
        {
            EnqueueClear(CachedContentType.Thumbnail);
        }
        else
        {
            EnforceThumbnailLimit();
            EnqueueNextThumbnail();
        }
    }

    /// <summary>
    /// Starts or stops extracting the characters of every page that has none yet, one page at a time.
    /// </summary>
    public void SetTextExtractionSettings(bool extractText)
    {
        _extractText = extractText;
        EnqueueNextTextExtraction();
    }

    /// <summary>
    /// Clears all extracted content of every page after the work already enqueued.
    /// <see cref="ExtractedContentCleared"/> is raised when done.
    /// </summary>
    public void ClearExtractedContent() => EnqueueClear(CachedContentType.Words | CachedContentType.Thumbnail);

    /// <summary>
    /// Adds the specified 1-based page number to the requested thumbnails and starts producing its thumbnail as set by
    /// <see cref="SetThumbnailSettings"/>; <see cref="PageThumbnailRendered"/> is raised when it is produced.
    /// Returns <see langword="false"/> when the page has no thumbnail.
    /// </summary>
    public bool RequestThumbnail(int pageNumber)
    {
        PdfImage? pageThumbnail = GetPageThumbnail(pageNumber);

        if (pageThumbnail == null && !_renderPageThumbnails)
        {
            return false;
        }

        lock (_thumbnailRequestLocker)
        {
            if (!_thumbnailRequests.ContainsKey(pageNumber))
            {
                EnqueueThumbnailRequest(pageNumber, pageThumbnail);
            }
        }

        return true;
    }

    /// <summary>
    /// Removes the specified 1-based page number from the requested thumbnails and cancels producing its thumbnail.
    /// </summary>
    public void CancelThumbnail(int pageNumber)
    {
        lock (_thumbnailRequestLocker)
        {
            if (!_thumbnailRequests.TryGetValue(pageNumber, out IPdfCancellableExecutionObserver? observer))
            {
                return;
            }

            _thumbnailRequests.Remove(pageNumber);
            observer?.Cancel();
            observer?.Dispose();
        }
    }

    /// <summary>
    /// Returns the <see cref="PdfPanelPageInfo"/> for the specified 1-based page number.
    /// </summary>
    public PdfPanelPageInfo GetPageInfo(int pageNumber) => _cache[pageNumber - 1].PageInfo;

    /// <summary>
    /// Enqueues decoding of <paramref name="cacheEntry"/> for <paramref name="request"/> unless its pending decode pass covers the request.
    /// </summary>
    private void EnqueueContentUpdate(PdfPageCacheEntry cacheEntry, PagesDrawingRequest request)
    {
        PagesDrawingRequest? pendingRequest = cacheEntry.PendingRequest;

        if (pendingRequest != null && pendingRequest.CoversPage(cacheEntry.PageNumber, request))
        {
            return;
        }

        cacheEntry.InitializeForRendering(_observerFactory, request);
        _processingQueue.Enqueue(new PdfPageUpdateCacheWorkItem(
            cacheEntry,
            _document,
            DocumentLocker,
            _textExtractor,
            _textExtractionProperties,
            _contentExtractor,
            _contentExtractionParameters,
            request,
            OnPageUpdated,
            OnPageTextExtracted));
    }

    /// <summary>
    /// Enqueues decoding of the pages of <paramref name="request"/> and marks them as the most recently requested.
    /// </summary>
    private void EnqueueContentUpdates(PagesDrawingRequest request)
    {
        foreach (RequestPageInfo page in request.Pages)
        {
            EnqueueContentUpdate(_cache[page.PageNumber - 1], request);
            _contentCachePageNumbers.Remove(page.PageNumber);
            _contentCachePageNumbers.Add(page.PageNumber);
        }
    }

    private void CancelPages(IEnumerable<int> pageNumbers)
    {
        foreach (int pageNumber in pageNumbers.ToList())
        {
            _cache[pageNumber - 1].Cancel();
        }
    }

    /// <summary>
    /// Cancels the queued or running items of the requested thumbnails and of the text extraction and thumbnail chains.
    /// </summary>
    private void CancelBackgroundWork()
    {
        lock (_thumbnailRequestLocker)
        {
            foreach (int pageNumber in _thumbnailRequests.Keys.ToList())
            {
                IPdfCancellableExecutionObserver? observer = _thumbnailRequests[pageNumber];
                observer?.Cancel();
                observer?.Dispose();
                _thumbnailRequests[pageNumber] = null;
            }
        }

        IPdfCancellableExecutionObserver? textExtractionObserver = Interlocked.Exchange(ref _textExtractionObserver, null);

        if (textExtractionObserver != null)
        {
            textExtractionObserver.Cancel();
            textExtractionObserver.Dispose();
            Volatile.Write(ref _textExtractionPending, 0);
        }

        IPdfCancellableExecutionObserver? thumbnailChainObserver = Interlocked.Exchange(ref _thumbnailChainObserver, null);

        if (thumbnailChainObserver != null)
        {
            thumbnailChainObserver.Cancel();
            thumbnailChainObserver.Dispose();
            Volatile.Write(ref _thumbnailPending, 0);
        }
    }

    /// <summary>
    /// Cancels and clears the decoded content of the least recently requested pages that are not visible,
    /// until at most <paramref name="contentCacheSize"/> pages besides the visible ones hold decoded content.
    /// </summary>
    private void EvictContentCache(int contentCacheSize)
    {
        List<PdfPageCacheEntry> evictedEntries = [];
        int index = 0;

        while (_contentCachePageNumbers.Count - _visiblePageNumbers.Count > contentCacheSize && index < _contentCachePageNumbers.Count)
        {
            int pageNumber = _contentCachePageNumbers[index];

            if (_visiblePageNumbers.Contains(pageNumber))
            {
                index++;
                continue;
            }

            PdfPageCacheEntry evictedEntry = _cache[pageNumber - 1];
            evictedEntry.Cancel();
            evictedEntries.Add(evictedEntry);
            _contentCachePageNumbers.RemoveAt(index);
        }

        if (evictedEntries.Count > 0)
        {
            _processingQueue.Enqueue(new PdfPageClearCacheWorkItem(evictedEntries, DocumentLocker, CachedContentType.Content | CachedContentType.Annotations, null));
        }
    }

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
        _textExtractionObserver = observer;
        _processingQueue.Enqueue(new PdfPageExtractTextWorkItem(
            nextEntry,
            _document,
            DocumentLocker,
            _textExtractor,
            _textExtractionProperties,
            _optionalContentStates,
            observer,
            pageNumber => OnTextExtractionCompleted(pageNumber, observer)));
    }

    private void EnqueueClear(CachedContentType contentTypes)
        => _processingQueue.Enqueue(new PdfPageClearCacheWorkItem(_cache, DocumentLocker, contentTypes, OnExtractedContentCleared));

    private void EnqueueNextThumbnail()
    {
        if (!_produceThumbnails || Interlocked.CompareExchange(ref _thumbnailPending, 1, 0) != 0)
        {
            return;
        }

        PdfPageCacheEntry? nextEntry = FindNextEntryWithoutThumbnail();

        if (nextEntry == null)
        {
            Volatile.Write(ref _thumbnailPending, 0);
            return;
        }

        IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(nextEntry.PageNumber);
        _thumbnailChainObserver = observer;
        _processingQueue.Enqueue(CreateThumbnailWorkItem(nextEntry, GetPageThumbnail(nextEntry.PageNumber), observer, OnThumbnailChainItemCompleted));
    }

    private PdfPageThumbnailWorkItem CreateThumbnailWorkItem(
        PdfPageCacheEntry cacheEntry,
        PdfImage? pageThumbnail,
        IPdfCancellableExecutionObserver observer,
        Action<PageThumbnailRenderedEventArgs> onRendered)
    {
        return new(
            cacheEntry,
            _document,
            DocumentLocker,
            pageThumbnail,
            _thumbnailMaximumSize,
            _optionalContentStates,
            observer,
            onRendered);
    }

    private PdfImage? GetPageThumbnail(int pageNumber)
    {
        if (!_usePageThumbnails)
        {
            return null;
        }

        lock (DocumentLocker)
        {
            return _document.Pages[pageNumber - 1].Thumbnail;
        }
    }

    private void OnPageTextExtracted(int pageNumber) => PageTextExtracted?.Invoke(this, new PageTextExtractedEventArgs(pageNumber));

    private void OnPageThumbnailRendered(PageThumbnailRenderedEventArgs args)
    {
        args.Observer.Dispose();
        EnforceThumbnailLimit();

        lock (_thumbnailRequestLocker)
        {
            if (!_thumbnailRequests.TryGetValue(args.PageNumber, out IPdfCancellableExecutionObserver? observer) || observer != args.Observer)
            {
                return;
            }

            _thumbnailRequests.Remove(args.PageNumber);
        }

        PageThumbnailRendered?.Invoke(this, args);
    }

    /// <summary>
    /// Enqueues producing the requested thumbnail of the specified 1-based page number. Must be called under the thumbnail request lock.
    /// </summary>
    private void EnqueueThumbnailRequest(int pageNumber, PdfImage? pageThumbnail)
    {
        IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(pageNumber);
        _thumbnailRequests[pageNumber] = observer;
        _processingQueue.Enqueue(CreateThumbnailWorkItem(_cache[pageNumber - 1], pageThumbnail, observer, OnPageThumbnailRendered));
    }

    /// <summary>
    /// Enqueues again the requested thumbnails, whose work was cancelled by <see cref="CancelBackgroundWork"/>.
    /// </summary>
    private void RequeueThumbnailRequests()
    {
        lock (_thumbnailRequestLocker)
        {
            foreach (int pageNumber in _thumbnailRequests.Keys.ToList())
            {
                EnqueueThumbnailRequest(pageNumber, GetPageThumbnail(pageNumber));
            }
        }
    }

    private void OnThumbnailChainItemCompleted(PageThumbnailRenderedEventArgs args)
    {
        args.Observer.Dispose();
        EnforceThumbnailLimit();

        if (Interlocked.CompareExchange(ref _thumbnailChainObserver, null, args.Observer) != args.Observer)
        {
            return;
        }

        Volatile.Write(ref _thumbnailPending, 0);

        if (args.Png != null)
        {
            EnqueueNextThumbnail();
        }
    }

    /// <summary>
    /// Clears the thumbnails farthest from the thumbnail center page until at most <see cref="_maximumThumbnailCount"/> are stored.
    /// </summary>
    private void EnforceThumbnailLimit()
    {
        int centerIndex = _thumbnailCenterPageNumber - 1;
        List<int> storedIndexes = [];

        for (int index = 0; index < _cache.Length; index++)
        {
            if (_cache[index].Thumbnail.HasContent)
            {
                storedIndexes.Add(index);
            }
        }

        int excessCount = storedIndexes.Count - _maximumThumbnailCount;

        if (excessCount <= 0)
        {
            return;
        }

        foreach (int index in storedIndexes.OrderByDescending(storedIndex => Math.Abs(storedIndex - centerIndex)).Take(excessCount))
        {
            _cache[index].Clear(CachedContentType.Thumbnail);
        }
    }

    private void OnExtractedContentCleared(CachedContentType contentTypes)
    {
        ExtractedContentCleared?.Invoke(this, new ExtractedContentClearedEventArgs(contentTypes));
        EnqueueNextTextExtraction();
        EnqueueNextThumbnail();
    }

    private void OnTextExtractionCompleted(int pageNumber, IPdfCancellableExecutionObserver observer)
    {
        if (_cache[pageNumber - 1].Content.Words.HasContent)
        {
            OnPageTextExtracted(pageNumber);
        }

        if (Interlocked.CompareExchange(ref _textExtractionObserver, null, observer) != observer)
        {
            return;
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

            if (!cacheEntry.Content.Words.HasContent)
            {
                return cacheEntry;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the next page selected by <see cref="_thumbnailPreloadMode"/> that has no thumbnail yet:
    /// a visible page in drawing order, or the page nearest to the thumbnail center page, walking outward from it.
    /// When <see cref="_maximumThumbnailCount"/> thumbnails are stored, only a page nearer than the farthest stored one is returned.
    /// </summary>
    private PdfPageCacheEntry? FindNextEntryWithoutThumbnail()
    {
        PdfPanelThumbnailPreloadMode preloadMode = _thumbnailPreloadMode;

        if (preloadMode == PdfPanelThumbnailPreloadMode.None)
        {
            return null;
        }

        int centerIndex = _thumbnailCenterPageNumber - 1;
        int storedCount = 0;
        int? farthestIndex = null;

        for (int index = 0; index < _cache.Length; index++)
        {
            if (!_cache[index].Thumbnail.HasContent)
            {
                continue;
            }

            storedCount++;

            if (farthestIndex == null || Math.Abs(index - centerIndex) > Math.Abs(farthestIndex.Value - centerIndex))
            {
                farthestIndex = index;
            }
        }

        if (preloadMode == PdfPanelThumbnailPreloadMode.VisiblePages)
        {
            foreach (int pageNumber in _visiblePageNumbersInOrder)
            {
                int index = pageNumber - 1;

                if (NeedsThumbnail(index))
                {
                    return SelectThumbnailEntry(index, centerIndex, storedCount, farthestIndex);
                }
            }

            return null;
        }

        for (int distance = 0; distance < _cache.Length; distance++)
        {
            for (int direction = 0; direction < 2; direction++)
            {
                if (distance == 0 && direction == 1)
                {
                    continue;
                }

                int index = (direction == 0) ? centerIndex + distance : centerIndex - distance;

                if (index >= 0 && index < _cache.Length && NeedsThumbnail(index))
                {
                    return SelectThumbnailEntry(index, centerIndex, storedCount, farthestIndex);
                }
            }
        }

        return null;
    }

    private bool NeedsThumbnail(int index)
    {
        return !_cache[index].Thumbnail.HasContent
            && (_renderPageThumbnails || GetPageThumbnail(index + 1) != null);
    }

    /// <summary>
    /// Returns the entry at <paramref name="index"/> when another thumbnail can be stored or the thumbnail
    /// at <paramref name="farthestIndex"/> is farther from <paramref name="centerIndex"/>, otherwise <see langword="null"/>.
    /// </summary>
    private PdfPageCacheEntry? SelectThumbnailEntry(int index, int centerIndex, int storedCount, int? farthestIndex)
    {
        if (storedCount < _maximumThumbnailCount)
        {
            return _cache[index];
        }

        if (farthestIndex != null && Math.Abs(farthestIndex.Value - centerIndex) > Math.Abs(index - centerIndex))
        {
            return _cache[index];
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _extractText = false;
        _processingQueue.Dispose();
        _textExtractionObserver?.Dispose();
        _thumbnailChainObserver?.Dispose();

        foreach (PdfPageCacheEntry cacheEntry in _cache)
        {
            cacheEntry.Dispose();
        }
    }
}
