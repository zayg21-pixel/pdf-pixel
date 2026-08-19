using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Lightweight container for cached page rendering artifacts.
/// </summary>
public sealed class PdfPageCacheEntry : IDisposable
{
    private PdfAnnotationPopup[]? _annotations;
    private bool _disposed;

    /// <summary>
    /// Initializes a cache entry for the given page number, info, and annotations.
    /// </summary>
    public PdfPageCacheEntry(int pageNumber, in PdfPanelPageInfo pageInfo)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        PageNumber = pageNumber;
        PageInfo = pageInfo;
        Content = new PdfPageCacheEntryItem(pageNumber);
        AnnotationContent = new PdfPageCacheEntryItem(pageNumber);
    }

    /// <summary>
    /// 1-based page number this cache entry represents.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// General information about the page.
    /// </summary>
    public PdfPanelPageInfo PageInfo { get; }

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
    public PdfAnnotationPopup[] GetAnnotations(IPdfDocument document, object documentLocker)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        ThrowIfDisposed();

        if (_annotations == null)
        {
            lock (documentLocker)
            {
                _annotations = document.CreateAnnotationPopups(PageNumber);
            }
        }

        return _annotations;
    }

    /// <summary>
    /// True if this entry has a pending or completed decode request.
    /// </summary>
    public bool PendingRequest { get; private set; }

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
    public void InitializeForRendering(IPdfExecutionObserverFactory observerFactory)
    {
        if (observerFactory == null)
        {
            throw new ArgumentNullException(nameof(observerFactory));
        }

        ThrowIfDisposed();

        if (ParseObserver == null)
        {
            ParseObserver = observerFactory.CreateParseObserver(PageNumber);
        }

        ContentObserver?.Cancel();
        ContentObserver?.Dispose();
        ContentObserver = observerFactory.CreateContentObserver(PageNumber);

        PendingRequest = true;
    }

    /// <summary>
    /// Cancels any in-progress work and clears the observers. Safe to call from any thread.
    /// </summary>
    public void Cancel()
    {
        ThrowIfDisposed();

        ParseObserver?.Cancel();
        ParseObserver?.Dispose();
        ParseObserver = null;

        ContentObserver?.Cancel();
        ContentObserver?.Dispose();
        ContentObserver = null;

        PendingRequest = false;
    }

    /// <summary>
    /// Clears cached content. Must be called from the worker thread.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        Content.Clear();
        AnnotationContent.Clear();
    }

    /// <summary>
    /// Returns collection of content pictures for main content and annotations.
    /// </summary>
    public PdfContentPictures GetContentPictures()
    {
        ThrowIfDisposed();

        return new PdfContentPictures
        {
            Content = Content.ContentPicture,
            Annotations = AnnotationContent.ContentPicture,
            ContentFeatures = Content.Features,
            ContentCharacters = Content.Characters
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
        Clear();
        _disposed = true;
    }
}
