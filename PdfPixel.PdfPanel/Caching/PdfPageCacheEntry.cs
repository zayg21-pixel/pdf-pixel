using PdfPixel.Commands;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Thumbnails;
using System;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// Lightweight container for cached page rendering artifacts.
/// </summary>
internal sealed class PdfPageCacheEntry : IDisposable
{
    private readonly object _renderingLocker = new();
    private PdfAnnotationPopup[]? _annotations;
    private bool _disposed;

    /// <summary>
    /// Initializes a cache entry for the given page number.
    /// </summary>
    public PdfPageCacheEntry(int pageNumber)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        Content = new PdfPageCacheEntryItem(pageNumber);
        AnnotationContent = new PdfPageCacheEntryItem(pageNumber);
        Thumbnail.SetContent(new PdfPageThumbnail());
    }

    /// <summary>
    /// Main page content.
    /// </summary>
    public PdfPageCacheEntryItem Content { get; }

    /// <summary>
    /// Page annotation content.
    /// </summary>
    public PdfPageCacheEntryItem AnnotationContent { get; }

    /// <summary>
    /// Rasterized tiles of the page's visible region, cleared with the content.
    /// </summary>
    public ContentLocker<PdfPageTiles> Tiles { get; } = new();

    /// <summary>
    /// Thumbnail of the page in the forms produced so far, always set; its forms are changed under the write lock.
    /// Kept when the content is cleared.
    /// </summary>
    public ContentLocker<PdfPageThumbnail> Thumbnail { get; } = new();

    /// <summary>
    /// Returns the forms the page's thumbnail was produced in.
    /// </summary>
    public PdfPanelThumbnailFormats GetThumbnailFormats()
    {
        using LockedContent<PdfPageThumbnail> lockedThumbnail = Thumbnail.GetContent();

        if (lockedThumbnail.Content == null)
        {
            return PdfPanelThumbnailFormats.None;
        }

        return lockedThumbnail.Content.Formats;
    }

    /// <summary>
    /// Returns the page's annotation popups, creating them on first call.
    /// </summary>
    public PdfAnnotationPopup[] GetAnnotations(PdfPanelPage page)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        ThrowIfDisposed();

        if (_annotations == null)
        {
            lock (page.PanelDocument.DocumentLocker)
            {
                _annotations = page.PanelDocument.CreateAnnotationPopups(page.PageNumber);
            }
        }

        return _annotations;
    }

    /// <summary>
    /// Observer used during page command recording. Held here so the provider can cancel it.
    /// </summary>
    public IPdfCancellableExecutionObserver? ParseObserver { get; private set; }

    /// <summary>
    /// Observer used during content picture rendering. Held here so the provider can cancel it.
    /// </summary>
    public IPdfCancellableExecutionObserver? ContentObserver { get; private set; }

    /// <summary>
    /// Prepares the entry for a new decode pass.
    /// Keeps the parse observer if one already exists, creating it only when missing;
    /// always cancels and replaces the content observer.
    /// </summary>
    public void InitializeForRendering(IPdfExecutionObserverFactory observerFactory, int pageNumber)
    {
        if (observerFactory == null)
        {
            throw new ArgumentNullException(nameof(observerFactory));
        }

        ThrowIfDisposed();

        lock (_renderingLocker)
        {
            if (ParseObserver == null)
            {
                ParseObserver = observerFactory.CreateParseObserver(pageNumber);
            }

            ContentObserver?.Cancel();
            ContentObserver?.Dispose();
            ContentObserver = observerFactory.CreateContentObserver(pageNumber);
        }
    }


    /// <summary>
    /// Cancels any in-progress work and clears the observers. Safe to call from any thread.
    /// </summary>
    public void Cancel()
    {
        ThrowIfDisposed();

        lock (_renderingLocker)
        {
            ParseObserver?.Cancel();
            ParseObserver?.Dispose();
            ParseObserver = null;

            ContentObserver?.Cancel();
            ContentObserver?.Dispose();
            ContentObserver = null;
        }
    }

    /// <summary>
    /// Clears the given types of cached content. Must be called from a work item.
    /// </summary>
    public void Clear(CachedContentType contentTypes)
    {
        ThrowIfDisposed();

        if ((contentTypes & CachedContentType.Content) != 0)
        {
            Content.ClearContent();
            AnnotationContent.ClearContent();
            Tiles.SetContent(null);
        }

        if ((contentTypes & CachedContentType.Words) != 0)
        {
            Content.Words.SetContent(null);
        }

        if ((contentTypes & CachedContentType.Thumbnail) != 0)
        {
            using WriteLockedContent<PdfPageThumbnail> lockedThumbnail = Thumbnail.GetContentForWriting();

            if (lockedThumbnail.Content != null)
            {
                lockedThumbnail.Content.Png = null;
                lockedThumbnail.Content.Samples = null;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdfPageCacheEntry));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Cancel();
        Clear(CachedContentType.All);
        Tiles.Dispose();
        Thumbnail.Dispose();
        _disposed = true;
    }
}
