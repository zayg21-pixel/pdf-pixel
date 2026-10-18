using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.Thumbnails;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Scheduling;

internal sealed partial class PdfPanelScheduler
{
    private readonly object _thumbnailLocker = new();
    private readonly Dictionary<int, IPdfCancellableExecutionObserver?> _thumbnailRequestsObservers = [];
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
    /// Starts producing the thumbnail of the specified 1-based page number; <see cref="PdfPanelDocument.PageThumbnailRendered"/> is raised when it is produced.
    /// </summary>
    public void RequestThumbnail(int pageNumber)
    {
        lock (_thumbnailLocker)
        {
            if (_thumbnailRequestsObservers.ContainsKey(pageNumber))
            {
                return;
            }

            EnqueueThumbnailRequest(pageNumber, GetPageThumbnail(pageNumber));
        }
    }

    /// <summary>
    /// Cancels producing the thumbnail of the specified 1-based page number.
    /// </summary>
    public void CancelThumbnail(int pageNumber)
    {
        lock (_thumbnailLocker)
        {
            if (!_thumbnailRequestsObservers.TryGetValue(pageNumber, out IPdfCancellableExecutionObserver? observer))
            {
                return;
            }

            _thumbnailRequestsObservers.Remove(pageNumber);
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

            PdfPageCacheEntry? nextEntry = FindNextEntryWithoutThumbnail(centerPageNumber.Value);

            if (nextEntry == null)
            {
                return;
            }

            IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(nextEntry.PageNumber);
            _thumbnailPreloadObserver = observer;
            _processingQueue.Enqueue(CreateThumbnailWorkItem(nextEntry, GetPageThumbnail(nextEntry.PageNumber), observer, OnThumbnailPreloadCompleted));
        }
    }

    private PdfPageThumbnailWorkItem CreateThumbnailWorkItem(
        PdfPageCacheEntry cacheEntry,
        PdfImage? pageThumbnail,
        IPdfCancellableExecutionObserver observer,
        Action<BackgroundWorkItemComplete> onCompleted)
    {
        return new(
            cacheEntry,
            _panelDocument.Document,
            _panelDocument.DocumentLocker,
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

        lock (_panelDocument.DocumentLocker)
        {
            return _panelDocument.Document.Pages[pageNumber - 1].Thumbnail;
        }
    }

    private void OnThumbnailRequestCompleted(BackgroundWorkItemComplete complete)
    {
        if (complete.Result == BackgroundWorkItemResult.Cancelled)
        {
            return;
        }

        byte[]? png = null;

        lock (_thumbnailLocker)
        {
            if (_thumbnailRequestsObservers.TryGetValue(complete.PageNumber, out IPdfCancellableExecutionObserver? observer)
                && observer == complete.Observer)
            {
                _thumbnailRequestsObservers.Remove(complete.PageNumber);

                using LockedContent<byte[]> lockedThumbnail = _panelDocument.Entries[complete.PageNumber - 1].Thumbnail.GetContent();
                png = lockedThumbnail.Content;
            }

            EnforceThumbnailLimit();
        }

        if (png != null)
        {
            _panelDocument.NotifyPageThumbnailRendered(new PageThumbnailRenderedEventArgs(complete.PageNumber, png));
        }
    }

    private void EnqueueThumbnailRequest(int pageNumber, PdfImage? pageThumbnail)
    {
        IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(pageNumber);
        _thumbnailRequestsObservers[pageNumber] = observer;
        _processingQueue.Enqueue(CreateThumbnailWorkItem(_panelDocument.Entries[pageNumber - 1], pageThumbnail, observer, OnThumbnailRequestCompleted));
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
    /// Clears the thumbnails farthest from the thumbnail center page beyond the maximum count.
    /// </summary>
    private void EnforceThumbnailLimit()
    {
        int? centerPageNumber = _thumbnailCenterPageNumber;

        if (centerPageNumber == null)
        {
            return;
        }

        IReadOnlyList<PdfPageCacheEntry> entries = _panelDocument.Entries;
        int centerIndex = centerPageNumber.Value - 1;
        List<int> storedIndexes = [];

        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index].Thumbnail.HasContent)
            {
                storedIndexes.Add(index);
            }
        }

        int excessCount = storedIndexes.Count - _thumbnailSettings.MaximumCount;

        if (excessCount <= 0)
        {
            return;
        }

        foreach (int index in storedIndexes.OrderByDescending(storedIndex => Math.Abs(storedIndex - centerIndex)).Take(excessCount))
        {
            entries[index].Clear(CachedContentType.Thumbnail);
        }
    }

    /// <summary>
    /// Returns the next page without a thumbnail selected by the preload mode, or <see langword="null"/>.
    /// </summary>
    private PdfPageCacheEntry? FindNextEntryWithoutThumbnail(int centerPageNumber)
    {
        PdfPanelThumbnailPreloadMode preloadMode = _thumbnailSettings.PreloadMode;

        if (preloadMode == PdfPanelThumbnailPreloadMode.None)
        {
            return null;
        }

        IReadOnlyList<PdfPageCacheEntry> entries = _panelDocument.Entries;
        int centerIndex = centerPageNumber - 1;
        int storedCount = 0;
        int? farthestIndex = null;

        for (int index = 0; index < entries.Count; index++)
        {
            if (!entries[index].Thumbnail.HasContent)
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
            foreach (int pageNumber in _thumbnailVisiblePageNumbers)
            {
                int index = pageNumber - 1;

                if (!entries[index].Thumbnail.HasContent)
                {
                    return SelectThumbnailEntry(index, centerIndex, storedCount, farthestIndex);
                }
            }

            return null;
        }

        for (int distance = 0; distance < entries.Count; distance++)
        {
            for (int direction = 0; direction < 2; direction++)
            {
                if (distance == 0 && direction == 1)
                {
                    continue;
                }

                int index = (direction == 0) ? centerIndex + distance : centerIndex - distance;

                if (index >= 0 && index < entries.Count && !entries[index].Thumbnail.HasContent)
                {
                    return SelectThumbnailEntry(index, centerIndex, storedCount, farthestIndex);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the entry at <paramref name="index"/> when its thumbnail fits within the maximum count, otherwise <see langword="null"/>.
    /// </summary>
    private PdfPageCacheEntry? SelectThumbnailEntry(int index, int centerIndex, int storedCount, int? farthestIndex)
    {
        if (storedCount < _thumbnailSettings.MaximumCount)
        {
            return _panelDocument.Entries[index];
        }

        if (farthestIndex != null && Math.Abs(farthestIndex.Value - centerIndex) > Math.Abs(index - centerIndex))
        {
            return _panelDocument.Entries[index];
        }

        return null;
    }
}
