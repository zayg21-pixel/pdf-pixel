using PdfPixel.Commands;
using PdfPixel.PdfPanel.Annotations;
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
        }

        if ((contentTypes & CachedContentType.Words) != 0)
        {
            Content.Words.SetContent(null);
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
        _disposed = true;
    }
}
