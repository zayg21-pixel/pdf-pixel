using PdfPixel.Commands;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Rendering;
using System;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// Lightweight container for cached page rendering artifacts.
/// </summary>
internal sealed class PdfPageCacheEntry : IDisposable
{
    private readonly object _renderingLocker = new();
    private PdfAnnotationPopup[]? _annotations;
    private PagesDrawingRequest? _pendingRequest;
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
    /// Thumbnail of the page as a PNG file. Kept when the content is cleared.
    /// </summary>
    public ContentLocker<byte[]> Thumbnail { get; } = new();

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
    /// Request the queued or running decode pass of this entry works for, or <see langword="null"/> when none is pending.
    /// </summary>
    public PagesDrawingRequest? PendingRequest
    {
        get
        {
            lock (_renderingLocker)
            {
                return _pendingRequest;
            }
        }
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
    /// Prepares the entry for a new decode pass for <paramref name="request"/>.
    /// Keeps the parse observer if one already exists, creating it only when missing;
    /// always cancels and replaces the content observer.
    /// </summary>
    public void InitializeForRendering(IPdfExecutionObserverFactory observerFactory, PagesDrawingRequest request, int pageNumber)
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
            _pendingRequest = request ?? throw new ArgumentNullException(nameof(request));
        }
    }

    /// <summary>
    /// Ends the pending decode pass when <paramref name="contentObserver"/> is still the entry's content observer.
    /// </summary>
    public void CompleteRendering(IPdfCancellableExecutionObserver contentObserver)
    {
        lock (_renderingLocker)
        {
            if (ReferenceEquals(ContentObserver, contentObserver))
            {
                _pendingRequest = null;
            }
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
            _pendingRequest = null;
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
        }

        if ((contentTypes & CachedContentType.Annotations) != 0)
        {
            AnnotationContent.ClearContent();
        }

        if ((contentTypes & CachedContentType.Words) != 0)
        {
            Content.Words.SetContent(null);
        }

        if ((contentTypes & CachedContentType.Thumbnail) != 0)
        {
            Thumbnail.SetContent(null);
        }
    }

    /// <summary>
    /// Returns collection of content pictures for main content, annotations and thumbnail.
    /// </summary>
    public PdfContentPictures GetContentPictures()
    {
        ThrowIfDisposed();

        return new PdfContentPictures
        {
            Content = Content.ContentPicture,
            Annotations = AnnotationContent.ContentPicture,
            Thumbnail = Thumbnail
        };
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
        Thumbnail.Dispose();
        _disposed = true;
    }
}
