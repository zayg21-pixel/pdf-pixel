using PdfPixel.Imaging;
using PdfPixel.PdfPanel.Rendering;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Scheduling;

internal sealed partial class PdfPanelScheduler
{
    private readonly object _thumbnailLocker = new();
    private readonly Dictionary<int, IPdfCancellableExecutionObserver?> _thumbnailRequests = [];
    private PdfPanelThumbnailSchedulingSettings _thumbnailSettings;
    private PdfPageThumbnailRasterizer? _thumbnailRasterizer;

    /// <summary>
    /// Sets how thumbnails are produced, for the requests made from now on.
    /// </summary>
    public void SetThumbnailSettings(PdfPanelThumbnailSchedulingSettings settings)
    {
        lock (_thumbnailLocker)
        {
            _thumbnailSettings = settings;
        }
    }

    /// <summary>
    /// Starts producing the thumbnail of the specified 1-based page number;
    /// <see cref="PdfPanelDocument.ThumbnailRendered"/> is raised when it is produced.
    /// </summary>
    public void RequestThumbnail(int pageNumber)
    {
        lock (_thumbnailLocker)
        {
            EnqueueThumbnailRequest(pageNumber);
        }
    }

    /// <summary>
    /// Cancels producing the thumbnail of the specified 1-based page number.
    /// </summary>
    public void CancelThumbnail(int pageNumber)
    {
        lock (_thumbnailLocker)
        {
            if (_thumbnailRequests.TryGetValue(pageNumber, out IPdfCancellableExecutionObserver? observer))
            {
                _thumbnailRequests.Remove(pageNumber);
                observer?.Cancel();
                observer?.Dispose();
            }
        }
    }

    /// <summary>
    /// Cancels the queued or running thumbnail items, keeping their requests so that
    /// <see cref="RequeueThumbnailRequests"/> queues them again.
    /// </summary>
    private void CancelThumbnails()
    {
        lock (_thumbnailLocker)
        {
            foreach (int pageNumber in _thumbnailRequests.Keys.ToList())
            {
                IPdfCancellableExecutionObserver? observer = _thumbnailRequests[pageNumber];
                observer?.Cancel();
                observer?.Dispose();
                _thumbnailRequests[pageNumber] = null;
            }
        }
    }

    private void RequeueThumbnailRequests()
    {
        lock (_thumbnailLocker)
        {
            foreach (int pageNumber in _thumbnailRequests.Keys.ToList())
            {
                EnqueueThumbnailRequest(pageNumber);
            }
        }
    }

    private void EnqueueThumbnailRequest(int pageNumber)
    {
        IPdfCancellableExecutionObserver observer = _observerFactory.CreateContentObserver(pageNumber);
        _thumbnailRequests[pageNumber] = observer;
        _processingQueue.Enqueue(new PdfPageThumbnailWorkItem(
            _panelDocument.Pages[pageNumber - 1],
            GetPageThumbnail(pageNumber),
            GetThumbnailRasterizer(),
            _panelDocument.OptionalContentStates,
            observer,
            _panelDocument.NotifyThumbnailRendered));
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

    /// <summary>
    /// Completes the request <paramref name="thumbnail"/> was produced for, on the UI thread: returns whether that
    /// request is still the page's request, forgetting it when it is.
    /// </summary>
    public bool CompleteThumbnail(PdfPageRenderedThumbnail thumbnail)
    {
        lock (_thumbnailLocker)
        {
            if (!_thumbnailRequests.TryGetValue(thumbnail.PageNumber, out IPdfCancellableExecutionObserver? observer)
                || observer != thumbnail.Observer)
            {
                return false;
            }

            _thumbnailRequests.Remove(thumbnail.PageNumber);

            return true;
        }
    }
}
