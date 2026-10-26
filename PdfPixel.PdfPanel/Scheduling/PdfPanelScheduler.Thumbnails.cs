using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.PdfPanel.Caching;
using PdfPixel.PdfPanel.Thumbnails;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Scheduling;

internal sealed partial class PdfPanelScheduler
{
    private readonly object _thumbnailLocker = new();
    private readonly Dictionary<int, IPdfCancellableExecutionObserver?> _thumbnailRequestsObservers = [];
    private readonly HashSet<int> _pinnedThumbnailPageNumbers = [];
    private IPdfCancellableExecutionObserver? _thumbnailPreloadObserver;
    private PdfPanelThumbnailSchedulingSettings _thumbnailSettings;
    private int[] _thumbnailVisiblePageNumbers = [];
    private int? _thumbnailCenterPageNumber;

    /// <summary>
    /// Sets how thumbnails are produced; clears the thumbnails when their content changes.
    /// </summary>
    public void SetThumbnailSettings(PdfPanelThumbnailSchedulingSettings settings)
    {
        lock (_thumbnailLocker)
        {
            bool thumbnailsChanged = settings.UsePageThumbnails != _thumbnailSettings.UsePageThumbnails
                || settings.RenderMaximumSize != _thumbnailSettings.RenderMaximumSize;

            _thumbnailSettings = settings;

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
    }

    /// <summary>
    /// Starts producing the thumbnail of the specified 1-based page number unless it is ready or pending;
    /// <see cref="PdfPanelDocument.PageThumbnailRendered"/> is raised when it is produced.
    /// A pinned thumbnail is never evicted and is produced again after it is cleared, until it is released.
    /// </summary>
    public void RequestThumbnail(int pageNumber, bool pin)
    {
        lock (_thumbnailLocker)
        {
            if (pin)
            {
                _pinnedThumbnailPageNumbers.Add(pageNumber);
            }

            if (_thumbnailRequestsObservers.ContainsKey(pageNumber)
                || _panelDocument.Pages[pageNumber - 1].CacheEntry.Thumbnail.HasContent)
            {
                return;
            }

            EnqueueThumbnailRequest(pageNumber, GetPageThumbnail(pageNumber));
        }
    }

    /// <summary>
    /// Cancels producing the thumbnail of the specified 1-based page number and unpins it; a ready thumbnail is kept until it is evicted.
    /// </summary>
    public void ReleaseThumbnail(int pageNumber)
    {
        lock (_thumbnailLocker)
        {
            _pinnedThumbnailPageNumbers.Remove(pageNumber);

            if (_thumbnailRequestsObservers.TryGetValue(pageNumber, out IPdfCancellableExecutionObserver? observer))
            {
                _thumbnailRequestsObservers.Remove(pageNumber);
                observer?.Cancel();
                observer?.Dispose();
            }

            EnforceThumbnailLimit();
        }
    }

    /// <summary>
    /// Cancels the queued or running thumbnail items.
    /// </summary>
    private void CancelThumbnails()
    {
        lock (_thumbnailLocker)
        {
            foreach (int pageNumber in _thumbnailRequestsObservers.Keys.ToList())
            {
                IPdfCancellableExecutionObserver? observer = _thumbnailRequestsObservers[pageNumber];
                observer?.Cancel();
                observer?.Dispose();
                _thumbnailRequestsObservers[pageNumber] = null;
            }

            if (_thumbnailPreloadObserver == null)
            {
                return;
            }

            _thumbnailPreloadObserver.Cancel();
            _thumbnailPreloadObserver.Dispose();
            _thumbnailPreloadObserver = null;
        }
    }

    private void EnqueueNextThumbnail()
    {
        lock (_thumbnailLocker)
        {
            int? centerPageNumber = _thumbnailCenterPageNumber;

            if (centerPageNumber == null || _thumbnailPreloadObserver != null)
            {
                return;
            }

            PdfPanelPage? nextPage = FindNextPageWithoutThumbnail(centerPageNumber.Value);

            if (nextPage == null)
            {
                return;
            }

            IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(nextPage.PageNumber);
            _thumbnailPreloadObserver = observer;
            _processingQueue.Enqueue(CreateThumbnailWorkItem(nextPage, GetPageThumbnail(nextPage.PageNumber), observer, OnThumbnailPreloadCompleted));
        }
    }

    private PdfPageThumbnailWorkItem CreateThumbnailWorkItem(
        PdfPanelPage page,
        PdfImage? pageThumbnail,
        IPdfCancellableExecutionObserver observer,
        Action<BackgroundWorkItemComplete> onCompleted)
    {
        return new(
            page,
            pageThumbnail,
            _thumbnailSettings.RenderMaximumSize,
            _panelDocument.OptionalContentStates,
            observer,
            onCompleted);
    }

    private PdfImage? GetPageThumbnail(int pageNumber)
    {
        if (!_thumbnailSettings.UsePageThumbnails)
        {
            return null;
        }

        return _panelDocument.Pages[pageNumber - 1].PdfPage.Thumbnail;
    }

    private void OnThumbnailRequestCompleted(BackgroundWorkItemComplete complete)
    {
        if (complete.Result == BackgroundWorkItemResult.Cancelled)
        {
            return;
        }

        var isRendered = false;

        lock (_thumbnailLocker)
        {
            if (_thumbnailRequestsObservers.TryGetValue(complete.PageNumber, out IPdfCancellableExecutionObserver? observer)
                && observer == complete.Observer)
            {
                _thumbnailRequestsObservers.Remove(complete.PageNumber);
                isRendered = _panelDocument.Pages[complete.PageNumber - 1].CacheEntry.Thumbnail.HasContent;
            }

            EnforceThumbnailLimit();
        }

        if (isRendered)
        {
            _panelDocument.NotifyPageThumbnailRendered(new PageThumbnailRenderedEventArgs(complete.PageNumber));
        }
    }

    private void EnqueueThumbnailRequest(int pageNumber, PdfImage? pageThumbnail)
    {
        IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(pageNumber);
        _thumbnailRequestsObservers[pageNumber] = observer;
        _processingQueue.Enqueue(CreateThumbnailWorkItem(_panelDocument.Pages[pageNumber - 1], pageThumbnail, observer, OnThumbnailRequestCompleted));
    }

    private void RequeueThumbnailRequests()
    {
        lock (_thumbnailLocker)
        {
            foreach (int pageNumber in _thumbnailRequestsObservers.Keys.ToList())
            {
                EnqueueThumbnailRequest(pageNumber, GetPageThumbnail(pageNumber));
            }
        }
    }

    /// <summary>
    /// Starts producing the pinned thumbnails that are neither ready nor pending.
    /// </summary>
    private void RequestPinnedThumbnails()
    {
        lock (_thumbnailLocker)
        {
            foreach (int pageNumber in _pinnedThumbnailPageNumbers)
            {
                if (!_thumbnailRequestsObservers.ContainsKey(pageNumber)
                    && !_panelDocument.Pages[pageNumber - 1].CacheEntry.Thumbnail.HasContent)
                {
                    EnqueueThumbnailRequest(pageNumber, GetPageThumbnail(pageNumber));
                }
            }
        }
    }

    /// <summary>
    /// Whether the thumbnail at the 0-based <paramref name="index"/> is pinned or drawn in place of its visible page,
    /// so it is never evicted.
    /// </summary>
    private bool IsThumbnailKept(int index)
    {
        int pageNumber = index + 1;

        return _pinnedThumbnailPageNumbers.Contains(pageNumber)
            || (_thumbnailSettings.PlaceholderMode != PdfPanelThumbnailPlaceholderMode.None
                && Array.IndexOf(_thumbnailVisiblePageNumbers, pageNumber) >= 0);
    }

    private void OnThumbnailPreloadCompleted(BackgroundWorkItemComplete complete)
    {
        if (complete.Result == BackgroundWorkItemResult.Cancelled)
        {
            return;
        }

        lock (_thumbnailLocker)
        {
            EnforceThumbnailLimit();

            if (_thumbnailPreloadObserver != complete.Observer)
            {
                return;
            }

            _thumbnailPreloadObserver = null;

            if (complete.Result == BackgroundWorkItemResult.Success)
            {
                EnqueueNextThumbnail();
            }
        }
    }

    /// <summary>
    /// Clears the evictable thumbnails farthest from the thumbnail center page beyond the maximum count.
    /// </summary>
    private void EnforceThumbnailLimit()
    {
        int? centerPageNumber = _thumbnailCenterPageNumber;

        if (centerPageNumber == null)
        {
            return;
        }

        IReadOnlyList<PdfPanelPage> pages = _panelDocument.Pages;
        int centerIndex = centerPageNumber.Value - 1;
        int storedCount = 0;
        List<int> evictableIndexes = [];

        for (int index = 0; index < pages.Count; index++)
        {
            if (!pages[index].CacheEntry.Thumbnail.HasContent)
            {
                continue;
            }

            storedCount++;

            if (!IsThumbnailKept(index))
            {
                evictableIndexes.Add(index);
            }
        }

        int excessCount = storedCount - _thumbnailSettings.MaximumCount;

        if (excessCount <= 0)
        {
            return;
        }

        foreach (int index in evictableIndexes.OrderByDescending(evictableIndex => Math.Abs(evictableIndex - centerIndex)).Take(excessCount))
        {
            pages[index].CacheEntry.Clear(CachedContentType.Thumbnail);
        }
    }

    /// <summary>
    /// Returns the next page without a thumbnail selected by the placeholder mode, or <see langword="null"/>.
    /// </summary>
    private PdfPanelPage? FindNextPageWithoutThumbnail(int centerPageNumber)
    {
        PdfPanelThumbnailPlaceholderMode placeholderMode = _thumbnailSettings.PlaceholderMode;

        if (placeholderMode == PdfPanelThumbnailPlaceholderMode.None)
        {
            return null;
        }

        IReadOnlyList<PdfPanelPage> pages = _panelDocument.Pages;
        int centerIndex = centerPageNumber - 1;
        int storedCount = 0;
        int? farthestIndex = null;

        for (int index = 0; index < pages.Count; index++)
        {
            if (!pages[index].CacheEntry.Thumbnail.HasContent)
            {
                continue;
            }

            storedCount++;

            if (IsThumbnailKept(index))
            {
                continue;
            }

            if (farthestIndex == null || Math.Abs(index - centerIndex) > Math.Abs(farthestIndex.Value - centerIndex))
            {
                farthestIndex = index;
            }
        }

        if (placeholderMode == PdfPanelThumbnailPlaceholderMode.VisiblePages)
        {
            foreach (int pageNumber in _thumbnailVisiblePageNumbers)
            {
                int index = pageNumber - 1;

                if (!pages[index].CacheEntry.Thumbnail.HasContent)
                {
                    return SelectThumbnailPage(index, centerIndex, storedCount, farthestIndex);
                }
            }

            return null;
        }

        for (int distance = 0; distance < pages.Count; distance++)
        {
            for (int direction = 0; direction < 2; direction++)
            {
                if (distance == 0 && direction == 1)
                {
                    continue;
                }

                int index = (direction == 0) ? centerIndex + distance : centerIndex - distance;

                if (index >= 0 && index < pages.Count && !pages[index].CacheEntry.Thumbnail.HasContent)
                {
                    return SelectThumbnailPage(index, centerIndex, storedCount, farthestIndex);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the page at <paramref name="index"/> when its thumbnail is kept or fits within the maximum count, otherwise <see langword="null"/>.
    /// </summary>
    private PdfPanelPage? SelectThumbnailPage(int index, int centerIndex, int storedCount, int? farthestIndex)
    {
        if (storedCount < _thumbnailSettings.MaximumCount || IsThumbnailKept(index))
        {
            return _panelDocument.Pages[index];
        }

        if (farthestIndex != null && Math.Abs(farthestIndex.Value - centerIndex) > Math.Abs(index - centerIndex))
        {
            return _panelDocument.Pages[index];
        }

        return null;
    }
}
