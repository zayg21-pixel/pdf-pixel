using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Execution;
using System;

namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// Page thumbnails of the panel: the page's thumbnail image (/Thumb) or a rendering of the page, as PNG files.
/// </summary>
public sealed class PdfPanelThumbnails
{
    private const int DefaultRenderMaximumSize = 200;

    private readonly PdfPageContentProvider _contentProvider;
    private readonly PdfPanelLayers _layers;
    private readonly IPdfCancellableExecutionObserver?[] _pendingObservers;
    private readonly byte[]?[] _pngs;
    private bool _usePageThumbnails = true;
    private bool _renderPages = true;
    private PdfIntegerSize _renderMaximumSize = new(DefaultRenderMaximumSize, DefaultRenderMaximumSize);

    internal PdfPanelThumbnails(PdfPageContentProvider contentProvider, PdfPanelLayers layers)
    {
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
        _pendingObservers = new IPdfCancellableExecutionObserver?[contentProvider.GetPagesCount()];
        _pngs = new byte[]?[contentProvider.GetPagesCount()];
    }

    /// <summary>
    /// Raised when the thumbnail of a requested page is ready.
    /// </summary>
    public event EventHandler<PdfPanelThumbnailEventArgs>? ThumbnailReady;

    /// <summary>
    /// Raised when every thumbnail was dropped by <see cref="Clear"/>.
    /// </summary>
    public event EventHandler? Cleared;

    /// <summary>
    /// Whether the page's thumbnail image (/Thumb) is used when present. Changing it clears the thumbnails.
    /// </summary>
    public bool UsePageThumbnails
    {
        get => _usePageThumbnails;

        set
        {
            if (_usePageThumbnails != value)
            {
                _usePageThumbnails = value;
                Clear();
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
                Clear();
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

        PdfIntegerSize renderSize = GetRenderSize(pageNumber);

        return new PdfSize(renderSize.Width, renderSize.Height);
    }

    /// <summary>
    /// Returns the thumbnail of a page as a PNG file, or <see langword="null"/> when it is not ready.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public byte[]? GetPng(int pageNumber) => _pngs[pageNumber - 1];

    /// <summary>
    /// Starts producing the thumbnail of a page, unless it is ready, pending, or the page has none.
    /// <see cref="ThumbnailReady"/> is raised when it is ready.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    public void Request(int pageNumber)
    {
        int index = pageNumber - 1;

        if (_pngs[index] != null || _pendingObservers[index] != null)
        {
            return;
        }

        PdfImage? pageThumbnail = GetPageThumbnail(pageNumber);
        if (pageThumbnail == null && !_renderPages)
        {
            return;
        }

        _pendingObservers[index] = _contentProvider.RequestThumbnail(
            pageNumber,
            pageThumbnail,
            _renderMaximumSize,
            GetRenderSize(pageNumber),
            GetRenderScale(pageNumber),
            _layers.Configuration?.ToStates());
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
    /// Drops every thumbnail and cancels the pending ones.
    /// </summary>
    public void Clear()
    {
        for (int pageNumber = 1; pageNumber <= _pngs.Length; pageNumber++)
        {
            Cancel(pageNumber);
        }

        Array.Clear(_pngs, 0, _pngs.Length);
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stores a produced thumbnail when it belongs to the pending request of its page.
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
        _pngs[index] = args.Png;
        ThumbnailReady?.Invoke(this, new PdfPanelThumbnailEventArgs(args.PageNumber));
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

    private float GetRenderScale(int pageNumber)
    {
        PdfRectangle cropBox = _contentProvider.GetPageInfo(pageNumber).CropBox;

        return Math.Min(_renderMaximumSize.Width / cropBox.Width, _renderMaximumSize.Height / cropBox.Height);
    }

    private PdfIntegerSize GetRenderSize(int pageNumber)
    {
        PdfRectangle cropBox = _contentProvider.GetPageInfo(pageNumber).CropBox;
        float scale = GetRenderScale(pageNumber);

        return new PdfIntegerSize(
            Math.Max(1, (int)Math.Round(cropBox.Width * scale)),
            Math.Max(1, (int)Math.Round(cropBox.Height * scale)));
    }
}
