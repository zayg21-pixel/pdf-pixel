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

    private readonly HashSet<int> _requestedPageNumbers = [];
    private readonly HashSet<int> _contentCachePageNumbers = [];
    private PdfPageTilePool? _tilePool;
    private readonly PdfContentExtractor _contentExtractor = new();
    private readonly PdfContentExtractionParameters _contentExtractionParameters = new() { ContentKinds = ExtractedContentKinds };

    /// <summary>
    /// Cancels and evicts the content of the pages <paramref name="request"/> no longer holds, then starts decoding its
    /// pages in their order.
    /// </summary>
    public void UpdateContent(ContentUpdateRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (_tilePool == null || _tiler == null || _tilePool.TileSize != request.TileSize)
        {
            _tiler?.Dispose();
            _tilePool = new PdfPageTilePool(request.TileSize);
            _tiler = new PdfPageContentTiler(_panelDocument, _tilePool, _surfaceParameters);
        }

        _tilePool.UpdateCapacity(request.TileCapacity);

        HashSet<int> requestedPageNumbers = new(request.Pages.Select(page => page.Page.PageNumber));
        int[] visiblePageNumbers = request.Pages.Where(page => page.IsVisible).Select(page => page.Page.PageNumber).ToArray();

        CancelThumbnails();
        CancelTextExtraction();

        CancelPages(_requestedPageNumbers.Except(requestedPageNumbers));
        _requestedPageNumbers.Clear();
        _requestedPageNumbers.UnionWith(requestedPageNumbers);

        if (visiblePageNumbers.Length > 0)
        {
            lock (_thumbnailLocker)
            {
                _thumbnailVisiblePageNumbers = visiblePageNumbers;
                _thumbnailCenterPageNumber = visiblePageNumbers[0];
            }

            lock (_textExtractionLocker)
            {
                _textExtractionStartPageNumber = visiblePageNumbers[0];
            }
        }

        EvictContentCache();

        foreach (ContentUpdatePage page in request.Pages)
        {
            EnqueueContentUpdate(page, request);
        }

        RequeueThumbnailRequests();
        EnqueueNextThumbnail();
        EnqueueNextTextExtraction();
    }

    private void EnqueueContentUpdate(ContentUpdatePage requestPage, ContentUpdateRequest request)
    {
        PdfPanelPage page = requestPage.Page;

        _contentCachePageNumbers.Add(page.PageNumber);
        page.CacheEntry.InitializeForRendering(_observerFactory, page.PageNumber);
        _processingQueue.Enqueue(new PdfPageUpdateCacheWorkItem(
            requestPage,
            _tiler,
            _textExtractor,
            _textExtractionProperties,
            _contentExtractor,
            _contentExtractionParameters,
            _surfaceParameters,
            request,
            _panelDocument.NotifyPageUpdated,
            _panelDocument.NotifyPageTextExtracted));
    }

    private void CancelPages(IEnumerable<int> pageNumbers)
    {
        foreach (int pageNumber in pageNumbers.ToList())
        {
            _panelDocument.Pages[pageNumber - 1].CacheEntry.Cancel();
        }
    }

    /// <summary>
    /// Clears the decoded content of the pages that are not in the last request.
    /// </summary>
    private void EvictContentCache()
    {
        List<PdfPanelPage> evictedPages = [];

        foreach (int pageNumber in _contentCachePageNumbers)
        {
            if (_requestedPageNumbers.Contains(pageNumber))
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
            _processingQueue.Enqueue(new PdfPageClearCacheWorkItem(evictedPages, _panelDocument.DocumentLocker, CachedContentType.Content, null));
        }
    }
}
