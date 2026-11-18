using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Scheduling;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// Page thumbnails of the panel, the only cache of them: the page's embedded thumbnail image or a rendering of the
/// page, kept as the host's pixels from its <see cref="IPdfPanelThumbnailResourceFactory"/>. The host requests the
/// thumbnails it needs and cancels the ones it no longer needs; the cancelled ones are evicted beyond
/// <see cref="MaximumCount"/>, the least recently cancelled first.
/// </summary>
public sealed class PdfPanelThumbnails : IDisposable
{
    internal const bool DefaultUsePageThumbnails = false;
    internal const int DefaultMaximumCount = 50;

    internal static readonly PdfIntegerSize DefaultRenderMaximumSize = new(400, 400);

    private readonly PdfPanelDocument _panelDocument;
    private readonly PdfPanelScheduler _scheduler;
    private readonly IPdfPanelThumbnailResourceFactory _resourceFactory;
    private readonly Dictionary<int, PdfPageThumbnailEntry> _entries = [];
    private readonly List<int> _cancelledPageNumbers = [];
    private bool _usePageThumbnails = DefaultUsePageThumbnails;
    private PdfIntegerSize _renderMaximumSize = DefaultRenderMaximumSize;
    private int _maximumCount = DefaultMaximumCount;

    internal PdfPanelThumbnails(PdfPanelDocument panelDocument, PdfPanelScheduler scheduler, IPdfPanelThumbnailResourceFactory resourceFactory)
    {
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _resourceFactory = resourceFactory ?? throw new ArgumentNullException(nameof(resourceFactory));
        UpdateSchedulingSettings();
        _panelDocument.ThumbnailRendered += OnThumbnailRendered;
    }

    /// <summary>
    /// Raised on the UI thread when the thumbnail of a requested page was produced; it is read with <see cref="Get"/>.
    /// </summary>
    public event EventHandler<PdfPanelThumbnailEventArgs>? ThumbnailReady;

    /// <summary>
    /// Raised when every thumbnail was dropped, after a setting or the visibility of a layer changed; the requested
    /// ones are produced again.
    /// </summary>
    public event EventHandler? Cleared;

    /// <summary>
    /// Whether the page's embedded thumbnail image is used when present. Changing it clears the thumbnails.
    /// </summary>
    public bool UsePageThumbnails
    {
        get => _usePageThumbnails;

        set
        {
            if (_usePageThumbnails != value)
            {
                _usePageThumbnails = value;
                UpdateSchedulingSettings();
                Clear();
            }
        }
    }

    /// <summary>
    /// Size in pixels a thumbnail is fitted within, keeping its aspect ratio; a thumbnail image is only reduced.
    /// Changing it clears the thumbnails.
    /// </summary>
    public PdfIntegerSize RenderMaximumSize
    {
        get => _renderMaximumSize;

        set
        {
            if (_renderMaximumSize != value)
            {
                _renderMaximumSize = value;
                UpdateSchedulingSettings();
                Clear();
            }
        }
    }

    /// <summary>
    /// Maximum number of thumbnails kept; requested thumbnails are kept beyond it.
    /// </summary>
    public int MaximumCount
    {
        get => _maximumCount;

        set
        {
            if (_maximumCount != value)
            {
                _maximumCount = value;
                EvictCancelled();
            }
        }
    }

    /// <summary>
    /// Returns the size in pixels of the thumbnail of a page, or <see langword="null"/> when the page has none.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public PdfSize? GetSize(int pageNumber)
    {
        PdfImage? pageThumbnail = GetPageThumbnail(pageNumber);

        if (pageThumbnail != null)
        {
            return GetDecodedSize(pageThumbnail);
        }

        PdfIntegerSize renderSize = PdfPageThumbnailRasterizer.GetRenderSize(_panelDocument.Pages[pageNumber - 1].Size, _renderMaximumSize);

        return new PdfSize(renderSize.Width, renderSize.Height);
    }

    /// <summary>
    /// Returns the host's pixels of the thumbnail of a page, or <see langword="null"/> when it is not produced.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public IPdfPanelThumbnailResource? Get(int pageNumber) => (_entries.TryGetValue(pageNumber, out PdfPageThumbnailEntry? entry)) ? entry.Resource : null;

    /// <summary>
    /// Requests the thumbnail of a page: starts producing it unless it is produced or being produced, and keeps it
    /// until it is cancelled; <see cref="ThumbnailReady"/> is raised once it is produced.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public void Request(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > _panelDocument.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        if (!_entries.TryGetValue(pageNumber, out PdfPageThumbnailEntry? entry))
        {
            entry = new PdfPageThumbnailEntry();
            _entries.Add(pageNumber, entry);
        }

        entry.IsRequested = true;
        _cancelledPageNumbers.Remove(pageNumber);

        if (entry.Resource == null && !entry.IsPending)
        {
            entry.IsPending = true;
            _scheduler.RequestThumbnail(pageNumber);
        }
    }

    /// <summary>
    /// Cancels the request of the thumbnail of a page: stops producing it, and lets a produced one be evicted.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public void Cancel(int pageNumber)
    {
        if (!_entries.TryGetValue(pageNumber, out PdfPageThumbnailEntry? entry) || !entry.IsRequested)
        {
            return;
        }

        entry.IsRequested = false;

        if (entry.IsPending)
        {
            entry.IsPending = false;
            _scheduler.CancelThumbnail(pageNumber);
        }

        if (entry.Resource == null)
        {
            _entries.Remove(pageNumber);
        }
        else
        {
            _cancelledPageNumbers.Add(pageNumber);
            EvictCancelled();
        }
    }

    /// <summary>
    /// Drops every thumbnail and produces the requested ones again.
    /// </summary>
    internal void Clear()
    {
        List<int> pageNumbers = [.. _entries.Keys];

        foreach (int pageNumber in pageNumbers)
        {
            PdfPageThumbnailEntry entry = _entries[pageNumber];

            entry.Resource?.Dispose();
            entry.Resource = null;

            if (entry.IsPending)
            {
                _scheduler.CancelThumbnail(pageNumber);
            }

            if (entry.IsRequested)
            {
                entry.IsPending = true;
                _scheduler.RequestThumbnail(pageNumber);
            }
            else
            {
                _entries.Remove(pageNumber);
            }
        }

        _cancelledPageNumbers.Clear();
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Disposes the cancelled thumbnails while more than <see cref="MaximumCount"/> are kept, the least recently
    /// cancelled first.
    /// </summary>
    private void EvictCancelled()
    {
        while (_cancelledPageNumbers.Count > 0 && GetProducedCount() > _maximumCount)
        {
            int pageNumber = _cancelledPageNumbers[0];
            _cancelledPageNumbers.RemoveAt(0);
            _entries[pageNumber].Resource?.Dispose();
            _entries.Remove(pageNumber);
        }
    }

    private int GetProducedCount()
    {
        int producedCount = 0;

        foreach (PdfPageThumbnailEntry entry in _entries.Values)
        {
            if (entry.Resource != null)
            {
                producedCount++;
            }
        }

        return producedCount;
    }

    /// <summary>
    /// Ends the pending state of the page of <paramref name="thumbnail"/> when it completes the page's current request,
    /// keeping its pixels as the host's and raising <see cref="ThumbnailReady"/> when it was produced.
    /// </summary>
    private void OnThumbnailRendered(PdfPageRenderedThumbnail thumbnail)
    {
        if (!_scheduler.CompleteThumbnail(thumbnail) || !_entries.TryGetValue(thumbnail.PageNumber, out PdfPageThumbnailEntry? entry))
        {
            return;
        }

        entry.IsPending = false;

        if (thumbnail.Samples != null)
        {
            entry.Resource = _resourceFactory.Create(thumbnail.Samples);
            ThumbnailReady?.Invoke(this, new PdfPanelThumbnailEventArgs(thumbnail.PageNumber));
        }
    }

    private void UpdateSchedulingSettings()
    {
        PdfPanelThumbnailSchedulingSettings settings = new()
        {
            UsePageThumbnails = _usePageThumbnails,
            RenderMaximumSize = _renderMaximumSize
        };

        _scheduler.SetThumbnailSettings(settings);
    }

    private PdfImage? GetPageThumbnail(int pageNumber)
    {
        if (!_usePageThumbnails)
        {
            return null;
        }

        return _panelDocument.Pages[pageNumber - 1].PdfPage.Thumbnail;
    }

    /// <summary>
    /// Size <see cref="PdfImage.DecodePng"/> produces for <paramref name="pageThumbnail"/> with
    /// <see cref="RenderMaximumSize"/> as the maximum size.
    /// </summary>
    private PdfSize GetDecodedSize(PdfImage pageThumbnail)
    {
        float scale = Math.Min(
            (float)_renderMaximumSize.Width / pageThumbnail.Width,
            (float)_renderMaximumSize.Height / pageThumbnail.Height);

        if (scale >= 1)
        {
            return new PdfSize(pageThumbnail.Width, pageThumbnail.Height);
        }

        return new PdfSize(
            Math.Max(1, (int)(pageThumbnail.Width * scale)),
            Math.Max(1, (int)(pageThumbnail.Height * scale)));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _panelDocument.ThumbnailRendered -= OnThumbnailRendered;

        foreach (KeyValuePair<int, PdfPageThumbnailEntry> entry in _entries)
        {
            if (entry.Value.IsPending)
            {
                _scheduler.CancelThumbnail(entry.Key);
            }

            entry.Value.Resource?.Dispose();
        }

        _entries.Clear();
        _cancelledPageNumbers.Clear();
    }
}
