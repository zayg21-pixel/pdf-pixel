using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Execution;
using System;

namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// Page thumbnails of the panel: the page's embedded thumbnail image or a rendering of the page, as PNG files.
/// </summary>
public sealed class PdfPanelThumbnails
{
    private const int DefaultRenderMaximumSize = 200;
    private const int DefaultMaximumCount = 300;

    private readonly PdfPageContentProvider _contentProvider;
    private readonly IPdfCancellableExecutionObserver?[] _pendingObservers;
    private bool _usePageThumbnails = true;
    private bool _renderPages = true;
    private PdfIntegerSize _renderMaximumSize = new(DefaultRenderMaximumSize, DefaultRenderMaximumSize);
    private int _maximumCount = DefaultMaximumCount;

    internal PdfPanelThumbnails(PdfPageContentProvider contentProvider)
    {
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _pendingObservers = new IPdfCancellableExecutionObserver?[contentProvider.GetPagesCount()];
        _contentProvider.SetThumbnailSettings(_usePageThumbnails, _renderPages, _renderMaximumSize, _maximumCount);
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
                _contentProvider.SetThumbnailSettings(_usePageThumbnails, _renderPages, _renderMaximumSize, _maximumCount);
            }
        }
    }

    /// <summary>
    /// Whether pages without a used thumbnail image are rendered. Changing it clears the thumbnails.
    /// </summary>
    public bool RenderPages
    {
        get => _renderPages;

        set
        {
            if (_renderPages != value)
            {
                _renderPages = value;
                _contentProvider.SetThumbnailSettings(_usePageThumbnails, _renderPages, _renderMaximumSize, _maximumCount);
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
                _contentProvider.SetThumbnailSettings(_usePageThumbnails, _renderPages, _renderMaximumSize, _maximumCount);
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
                _contentProvider.SetThumbnailSettings(_usePageThumbnails, _renderPages, _renderMaximumSize, _maximumCount);
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

        if (!_renderPages)
        {
            return null;
        }

        PdfIntegerSize renderSize = PdfPageThumbnailWorkItem.GetRenderSize(_contentProvider.GetPageInfo(pageNumber).CropBox, _renderMaximumSize);

        return new PdfSize(renderSize.Width, renderSize.Height);
    }

    /// <summary>
    /// Returns the thumbnail of a page as a PNG file, or <see langword="null"/> when it is not ready.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public byte[]? GetPng(int pageNumber) => _contentProvider.GetThumbnail(pageNumber);

    /// <summary>
    /// Starts producing the thumbnail of a page, unless it is ready, pending, or the page has none.
    /// <see cref="ThumbnailReady"/> is raised when it is ready.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public void Request(int pageNumber)
    {
        int index = pageNumber - 1;

        if (_pendingObservers[index] != null || _contentProvider.GetThumbnail(pageNumber) != null)
        {
            return;
        }

        _pendingObservers[index] = _contentProvider.RequestThumbnail(pageNumber);
    }

    /// <summary>
    /// Cancels producing the thumbnail of a page; a ready thumbnail is kept.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public void Cancel(int pageNumber)
    {
        int index = pageNumber - 1;
        IPdfCancellableExecutionObserver? observer = _pendingObservers[index];

        if (observer == null)
        {
            return;
        }

        observer.Cancel();
        observer.Dispose();
        _pendingObservers[index] = null;
    }

    /// <summary>
    /// Cancels the pending thumbnails and raises <see cref="Cleared"/>.
    /// </summary>
    internal void OnExtractedContentCleared()
    {
        for (int pageNumber = 1; pageNumber <= _pendingObservers.Length; pageNumber++)
        {
            Cancel(pageNumber);
        }

        Cleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Completes the pending request of a page and raises <see cref="ThumbnailReady"/> when it produced a thumbnail.
    /// </summary>
    internal void OnPageThumbnailRendered(PageThumbnailRenderedEventArgs args)
    {
        int index = args.PageNumber - 1;

        if (_pendingObservers[index] != args.Observer)
        {
            return;
        }

        _pendingObservers[index] = null;
        args.Observer.Dispose();

        if (args.Png == null)
        {
            return;
        }

        ThumbnailReady?.Invoke(this, new PdfPanelThumbnailEventArgs(args.PageNumber, args.Png));
    }

    private PdfImage? GetPageThumbnail(int pageNumber)
    {
        if (!_usePageThumbnails)
        {
            return null;
        }

        lock (_contentProvider.DocumentLocker)
        {
            return _contentProvider.Document.Pages[pageNumber - 1].Thumbnail;
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
