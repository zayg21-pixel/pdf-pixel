using PdfPixel.Commands;
using PdfPixel.Content;
using PdfPixel.PdfPanel.Caching;
using PdfPixel.PdfPanel.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Scheduling;

internal sealed partial class PdfPanelScheduler
{
    private static readonly PdfContentKind[] ExtractedContentKinds = [PdfContentKind.Path, PdfContentKind.Image, PdfContentKind.Shading, PdfContentKind.Form];

    private readonly HashSet<int> _visiblePageNumbers = [];
    private readonly HashSet<int> _lookAheadPageNumbers = [];
    private readonly HashSet<int> _contentCachePageNumbers = [];
    private PagesDrawingRequest? _lastLookAheadRequest;
    private readonly PdfContentExtractor _contentExtractor = new();
    private readonly PdfContentExtractionParameters _contentExtractionParameters = new() { ContentKinds = ExtractedContentKinds };

    /// <summary>
    /// Starts or updates decoding for the pages described by <paramref name="request"/>.
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
            EvictContentCache();
            return;
        }

        CancelThumbnails();
        CancelTextExtraction();

        CancelPages(_lookAheadPageNumbers.Union(_visiblePageNumbers).Except(requestedPageNumbers));
        _visiblePageNumbers.Clear();
        _visiblePageNumbers.UnionWith(requestedPageNumbers);
        _lookAheadPageNumbers.ExceptWith(requestedPageNumbers);

        lock (_thumbnailLocker)
        {
            _thumbnailVisiblePageNumbers = request.Pages.Select(page => page.PageNumber).ToArray();
            _thumbnailCenterPageNumber = request.Pages[0].PageNumber;
        }

        lock (_textExtractionLocker)
        {
            _textExtractionStartPageNumber = request.Pages[0].PageNumber;
        }

        EnqueueContentUpdates(request);

        if (_lastLookAheadRequest != null)
        {
            foreach (int pageNumber in _lookAheadPageNumbers)
            {
                EnqueueContentUpdate(_panelDocument.Pages[pageNumber - 1], _lastLookAheadRequest);
            }
        }

        EvictContentCache();
        RequeueThumbnailRequests();
        EnqueueNextThumbnail();
        EnqueueNextTextExtraction();
    }

    private void EnqueueContentUpdate(PdfPanelPage page, PagesDrawingRequest request)
    {
        _contentCachePageNumbers.Add(page.PageNumber);

        PagesDrawingRequest? pendingRequest = page.CacheEntry.PendingRequest;

        if (pendingRequest != null && pendingRequest.CoversPage(page.PageNumber, request))
        {
            return;
        }

        page.CacheEntry.InitializeForRendering(_observerFactory, request, page.PageNumber);
        _processingQueue.Enqueue(new PdfPageUpdateCacheWorkItem(
            page,
            _textExtractor,
            _textExtractionProperties,
            _contentExtractor,
            _contentExtractionParameters,
            request,
            _panelDocument.NotifyPageUpdated,
            _panelDocument.NotifyPageTextExtracted));
    }

    private void EnqueueContentUpdates(PagesDrawingRequest request)
    {
        foreach (RequestPageInfo page in request.Pages)
        {
            EnqueueContentUpdate(_panelDocument.Pages[page.PageNumber - 1], request);
        }
    }

    private void CancelPages(IEnumerable<int> pageNumbers)
    {
        foreach (int pageNumber in pageNumbers.ToList())
        {
            _panelDocument.Pages[pageNumber - 1].CacheEntry.Cancel();
        }
    }

    /// <summary>
    /// Clears the decoded content of the pages that are neither visible nor ahead of the visible pages.
    /// </summary>
    private void EvictContentCache()
    {
        List<PdfPanelPage> evictedPages = [];

        foreach (int pageNumber in _contentCachePageNumbers)
        {
            if (_visiblePageNumbers.Contains(pageNumber) || _lookAheadPageNumbers.Contains(pageNumber))
            {
                continue;
            }

            PdfPanelPage evictedPage = _panelDocument.Pages[pageNumber - 1];
            evictedPage.CacheEntry.Cancel();
            evictedPages.Add(evictedPage);
        }

        foreach (PdfPanelPage evictedPage in evictedPages)
        {
            _contentCachePageNumbers.Remove(evictedPage.PageNumber);
        }

        if (evictedPages.Count > 0)
        {
            _processingQueue.Enqueue(new PdfPageClearCacheWorkItem(evictedPages, _panelDocument.DocumentLocker, CachedContentType.Content | CachedContentType.Annotations, null));
        }
    }
}
