using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.PdfPanel.Caching;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Thumbnails;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Scheduling;

internal sealed partial class PdfPanelScheduler
{
    private readonly object _thumbnailLocker = new();
    private readonly Dictionary<int, IPdfCancellableExecutionObserver?> _thumbnailRequestsObservers = [];
    private readonly Dictionary<int, PdfPanelThumbnailFormats> _thumbnailRequestsFormats = [];
    private readonly Dictionary<int, PdfPanelThumbnailFormats> _pinnedThumbnailFormats = [];
    private IPdfCancellableExecutionObserver? _thumbnailPreloadObserver;
    private PdfPanelThumbnailSchedulingSettings _thumbnailSettings;
    private PdfPageThumbnailRasterizer? _thumbnailRasterizer;
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
    /// Starts producing the <paramref name="formats"/> of the thumbnail of the specified 1-based page number unless they
    /// are ready or pending; <see cref="PdfPanelDocument.PageThumbnailRendered"/> is raised when they are produced.
    /// A pinned thumbnail is never evicted and its formats are produced again after it is cleared, until it is released.
    /// </summary>
    public void RequestThumbnail(int pageNumber, PdfPanelThumbnailFormats formats, bool pin)
    {
        lock (_thumbnailLocker)
        {
            if (pin)
            {
                _pinnedThumbnailFormats.TryGetValue(pageNumber, out PdfPanelThumbnailFormats pinnedFormats);
                _pinnedThumbnailFormats[pageNumber] = pinnedFormats | formats;
            }

            _thumbnailRequestsFormats.TryGetValue(pageNumber, out PdfPanelThumbnailFormats pendingFormats);

            if ((pendingFormats & formats) == formats
                || HasThumbnail(_panelDocument.Pages[pageNumber - 1], formats))
            {
                return;
            }

            CancelThumbnailRequest(pageNumber);
            EnqueueThumbnailRequest(pageNumber, pendingFormats | formats);
        }
    }

    /// <summary>
    /// Cancels producing the thumbnail of the specified 1-based page number and unpins it; a ready thumbnail is kept until it is evicted.
    /// </summary>
    public void ReleaseThumbnail(int pageNumber)
    {
        lock (_thumbnailLocker)
        {
            _pinnedThumbnailFormats.Remove(pageNumber);
            CancelThumbnailRequest(pageNumber);
            EnforceThumbnailLimit();
        }
    }

    /// <summary>
    /// Cancels the pending request of the thumbnail of the specified 1-based page number, if any, and forgets it.
    /// </summary>
    private void CancelThumbnailRequest(int pageNumber)
    {
        if (_thumbnailRequestsObservers.TryGetValue(pageNumber, out IPdfCancellableExecutionObserver? observer))
        {
            _thumbnailRequestsObservers.Remove(pageNumber);
            _thumbnailRequestsFormats.Remove(pageNumber);
            observer?.Cancel();
            observer?.Dispose();
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
            _processingQueue.Enqueue(CreateThumbnailWorkItem(nextPage, PdfPanelThumbnailFormats.Samples, GetPageThumbnail(nextPage.PageNumber), observer, OnThumbnailPreloadCompleted));
        }
    }

    private PdfPageThumbnailWorkItem CreateThumbnailWorkItem(
        PdfPanelPage page,
        PdfPanelThumbnailFormats formats,
        PdfImage? pageThumbnail,
        IPdfCancellableExecutionObserver observer,
        Action<BackgroundWorkItemComplete> onCompleted)
    {
        return new(
            page,
            formats,
            pageThumbnail,
            GetThumbnailRasterizer(),
            _panelDocument.OptionalContentStates,
            observer,
            onCompleted);
    }

    /// <summary>
    /// Returns the rasterizer every thumbnail is rendered with, created again when the maximum thumbnail size changes.
    /// </summary>
    private PdfPageThumbnailRasterizer GetThumbnailRasterizer()
    {
        PdfPageThumbnailRasterizer? rasterizer = _thumbnailRasterizer;

        if (rasterizer == null || rasterizer.MaximumSize != _thumbnailSettings.RenderMaximumSize)
        {
            rasterizer?.Dispose();
            rasterizer = new PdfPageThumbnailRasterizer(_thumbnailSettings.RenderMaximumSize, _panelDocument, _surfaceParameters);
            _thumbnailRasterizer = rasterizer;
        }

        return rasterizer;
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
                PdfPanelThumbnailFormats formats = _thumbnailRequestsFormats[complete.PageNumber];
                _thumbnailRequestsObservers.Remove(complete.PageNumber);
                _thumbnailRequestsFormats.Remove(complete.PageNumber);
                isRendered = HasThumbnail(_panelDocument.Pages[complete.PageNumber - 1], formats);
            }

            EnforceThumbnailLimit();
        }

        if (isRendered)
        {
            _panelDocument.NotifyPageThumbnailRendered(new PageThumbnailRenderedEventArgs(complete.PageNumber));
        }
    }

    private void EnqueueThumbnailRequest(int pageNumber, PdfPanelThumbnailFormats formats)
    {
        IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(pageNumber);
        _thumbnailRequestsObservers[pageNumber] = observer;
        _thumbnailRequestsFormats[pageNumber] = formats;
        _processingQueue.Enqueue(CreateThumbnailWorkItem(_panelDocument.Pages[pageNumber - 1], formats, GetPageThumbnail(pageNumber), observer, OnThumbnailRequestCompleted));
    }

    private void RequeueThumbnailRequests()
    {
        lock (_thumbnailLocker)
        {
            foreach (int pageNumber in _thumbnailRequestsObservers.Keys.ToList())
            {
                EnqueueThumbnailRequest(pageNumber, _thumbnailRequestsFormats[pageNumber]);
            }
        }
    }

    /// <summary>
    /// Starts producing the formats of pinned thumbnails that are neither ready nor pending.
    /// </summary>
    private void RequestPinnedThumbnails()
    {
        lock (_thumbnailLocker)
        {
            foreach (KeyValuePair<int, PdfPanelThumbnailFormats> pinned in _pinnedThumbnailFormats)
            {
                if (!_thumbnailRequestsObservers.ContainsKey(pinned.Key)
                    && !HasThumbnail(_panelDocument.Pages[pinned.Key - 1], pinned.Value))
                {
                    EnqueueThumbnailRequest(pinned.Key, pinned.Value);
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

        return _pinnedThumbnailFormats.ContainsKey(pageNumber)
            || (_thumbnailSettings.PlaceholderMode != PdfPanelThumbnailPlaceholderMode.None
                && Array.IndexOf(_thumbnailVisiblePageNumbers, pageNumber) >= 0);
    }

    /// <summary>
    /// Returns whether the thumbnail of <paramref name="page"/> was produced in every form of
    /// <paramref name="formats"/>.
    /// </summary>
    private static bool HasThumbnail(PdfPanelPage page, PdfPanelThumbnailFormats formats)
        => (page.CacheEntry.GetThumbnailFormats() & formats) == formats;

    /// <summary>
    /// Returns whether the thumbnail of <paramref name="page"/> was produced in any form.
    /// </summary>
    private static bool HasAnyThumbnail(PdfPanelPage page)
        => page.CacheEntry.GetThumbnailFormats() != PdfPanelThumbnailFormats.None;

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
            if (!HasAnyThumbnail(pages[index]))
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
    /// Returns the next page without thumbnail samples to draw as its placeholder, selected by the placeholder mode, or
    /// <see langword="null"/>.
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
            if (!HasAnyThumbnail(pages[index]))
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

                if (!HasThumbnail(pages[index], PdfPanelThumbnailFormats.Samples))
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

                if (index >= 0 && index < pages.Count && !HasThumbnail(pages[index], PdfPanelThumbnailFormats.Samples))
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
