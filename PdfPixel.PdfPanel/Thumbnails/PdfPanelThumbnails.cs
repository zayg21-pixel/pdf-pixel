using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.Scheduling;
using System;

namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// Page thumbnails of the panel: the page's embedded thumbnail image or a rendering of the page, as PNG files.
/// </summary>
public sealed class PdfPanelThumbnails
{
    internal const bool DefaultUsePageThumbnails = false;
    internal const int DefaultMaximumCount = 100;
    internal const PdfPanelThumbnailPreloadMode DefaultPreloadMode = PdfPanelThumbnailPreloadMode.VisiblePages;

    internal static readonly PdfIntegerSize DefaultRenderMaximumSize = new(400, 400);

    private readonly PdfPanelDocument _panelDocument;
    private readonly PdfPanelScheduler _scheduler;
    private bool _usePageThumbnails = DefaultUsePageThumbnails;
    private PdfIntegerSize _renderMaximumSize = DefaultRenderMaximumSize;
    private int _maximumCount = DefaultMaximumCount;
    private PdfPanelThumbnailPreloadMode _preloadMode = DefaultPreloadMode;

    internal PdfPanelThumbnails(PdfPanelDocument panelDocument, PdfPanelScheduler scheduler)
    {
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        UpdateSchedulingSettings();
    }

    /// <summary>
    /// Raised when the thumbnail of a requested page is ready.
    /// </summary>
    public event EventHandler<PdfPanelThumbnailEventArgs>? ThumbnailReady;

    /// <summary>
    /// Raised when every thumbnail was dropped, after a setting or the visibility of a layer changed.
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
            }
        }
    }

    /// <summary>
    /// Maximum number of thumbnails kept, the ones nearest to the first visible page.
    /// </summary>
    public int MaximumCount
    {
        get => _maximumCount;

        set
        {
            if (_maximumCount != value)
            {
                _maximumCount = value;
                UpdateSchedulingSettings();
            }
        }
    }

    /// <summary>
    /// Pages whose thumbnails are produced without being requested.
    /// </summary>
    public PdfPanelThumbnailPreloadMode PreloadMode
    {
        get => _preloadMode;

        set
        {
            if (_preloadMode != value)
            {
                _preloadMode = value;
                UpdateSchedulingSettings();
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

        PdfIntegerSize renderSize = PdfPageThumbnailWorkItem.GetRenderSize(_panelDocument.GetPageInfo(pageNumber).CropBox, _renderMaximumSize);

        return new PdfSize(renderSize.Width, renderSize.Height);
    }

    /// <summary>
    /// Returns the thumbnail of a page as a PNG file, or <see langword="null"/> when it is not ready.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public byte[]? GetPng(int pageNumber) => _panelDocument.GetThumbnail(pageNumber);

    /// <summary>
    /// Starts producing the thumbnail of a page, unless it is pending or the page has none.
    /// <see cref="ThumbnailReady"/> is raised when it is ready.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public void Request(int pageNumber) => _scheduler.RequestThumbnail(pageNumber);

    /// <summary>
    /// Cancels producing the thumbnail of a page; a ready thumbnail is kept.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public void Cancel(int pageNumber) => _scheduler.CancelThumbnail(pageNumber);

    /// <summary>
    /// Raises <see cref="Cleared"/>.
    /// </summary>
    internal void OnExtractedContentCleared() => Cleared?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Raises <see cref="ThumbnailReady"/> for the thumbnail produced by the request of a page.
    /// </summary>
    internal void OnPageThumbnailRendered(PageThumbnailRenderedEventArgs args)
        => ThumbnailReady?.Invoke(this, new PdfPanelThumbnailEventArgs(args.PageNumber, args.Png));

    private void UpdateSchedulingSettings()
    {
        PdfPanelThumbnailSchedulingSettings settings = new()
        {
            UsePageThumbnails = _usePageThumbnails,
            RenderMaximumSize = _renderMaximumSize,
            MaximumCount = _maximumCount,
            PreloadMode = _preloadMode
        };

        _scheduler.SetThumbnailSettings(settings);
    }

    private PdfImage? GetPageThumbnail(int pageNumber)
    {
        if (!_usePageThumbnails)
        {
            return null;
        }

        lock (_panelDocument.DocumentLocker)
        {
            return _panelDocument.Document.Pages[pageNumber - 1].Thumbnail;
        }
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
}
