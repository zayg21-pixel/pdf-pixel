using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Content;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Scheduling;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.WorkQueue;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Document of a panel and the content produced for its pages: decoded pictures, words,
/// content items and thumbnails. Raises the notifications of produced and cleared content on the synchronization context.
/// </summary>
public sealed class PdfPanelDocument : IDisposable
{
    private readonly PdfPanelPage[] _pages;
    private readonly object _disposeLocker = new();
    private bool _disposed;

    private PdfPanelDocument(
        IPdfDocument document,
        IWorkQueue processingQueue,
        IPdfExecutionObserverFactory observerFactory,
        SynchronizationContext? synchronizationContext)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        SynchronizationContext = synchronizationContext;
        _pages = new PdfPanelPage[document.Pages.Count];

        for (int index = 0; index < _pages.Length; index++)
        {
            _pages[index] = new PdfPanelPage(document.Pages[index], index + 1, this);
        }

        Scheduler = new PdfPanelScheduler(this, processingQueue, observerFactory);
    }

    /// <summary>
    /// Raised when a page's content or annotations have been decoded and are ready to render.
    /// </summary>
    internal event EventHandler<PageUpdatedArgs>? PageUpdated;

    /// <summary>
    /// Raised when a page's characters have been extracted, by text extraction or by rendering.
    /// </summary>
    internal event EventHandler<PageTextExtractedEventArgs>? PageTextExtracted;

    /// <summary>
    /// Raised when a requested thumbnail finished.
    /// </summary>
    internal event EventHandler<PageThumbnailRenderedEventArgs>? PageThumbnailRendered;

    /// <summary>
    /// Raised when extracted content of every page was cleared.
    /// </summary>
    internal event EventHandler<ExtractedContentClearedEventArgs>? ExtractedContentCleared;

    /// <summary>
    /// Document the pages are decoded from. Property reads are thread-safe;
    /// method calls and stream access must lock <see cref="DocumentLocker"/>.
    /// </summary>
    public IPdfDocument Document { get; }

    /// <summary>
    /// Lock object serializing method calls and stream access on <see cref="Document"/> and its pages.
    /// </summary>
    public object DocumentLocker { get; } = new();

    /// <summary>
    /// Pages of the document, in page order.
    /// </summary>
    public IReadOnlyList<PdfPanelPage> Pages => _pages;

    /// <summary>
    /// Synchronization context the notifications are posted to, or <see langword="null"/> when they are raised on the calling thread.
    /// </summary>
    internal SynchronizationContext? SynchronizationContext { get; }

    /// <summary>
    /// Scheduler that produces the content of the pages.
    /// </summary>
    internal PdfPanelScheduler Scheduler { get; }

    /// <summary>
    /// Optional content states the words and thumbnails are produced with.
    /// </summary>
    internal PdfOptionalContentStates? OptionalContentStates { get; set; }

    /// <summary>
    /// Returns the page if it exists.
    /// </summary>
    /// <param name="pageNumber">Number of the page.</param>
    /// <param name="page">Viewer page.</param>
    /// <returns>True if page found.</returns>
    public bool TryGetPage(int pageNumber, out PdfPanelPage? page)
    {
        if (pageNumber < 1 || pageNumber > _pages.Length)
        {
            page = null;
            return false;
        }

        page = _pages[pageNumber - 1];
        return true;
    }

    /// <summary>
    /// Gets the annotation popup at the specified page position.
    /// </summary>
    /// <param name="pageNumber">Number of the page.</param>
    /// <param name="pagePosition">Position on the page in PDF coordinates.</param>
    /// <returns>The annotation popup if found; otherwise, null.</returns>
    public PdfAnnotationPopup? GetAnnotationPopupAt(int pageNumber, in PdfPoint pagePosition)
    {
        if (!TryGetPage(pageNumber, out PdfPanelPage? page) || page == null)
        {
            return null;
        }

        return PdfDocumentAnnotationExtractor.GetActiveAnnotation(page, pagePosition);
    }

    /// <summary>
    /// Returns the currently cached <see cref="PdfContentPictures"/> for the specified 1-based page number.
    /// Returns empty pictures if the page has not been decoded yet.
    /// </summary>
    internal PdfContentPictures GetContentPictures(int pageNumber) => _pages[pageNumber - 1].CacheEntry.GetContentPictures();

    /// <summary>
    /// Returns the extracted words of the specified 1-based page number in reading order,
    /// or <see langword="null"/> if they have not been extracted yet.
    /// </summary>
    internal PdfWord[]? GetWords(int pageNumber)
    {
        using LockedContent<PdfWord[]> lockedWords = _pages[pageNumber - 1].CacheEntry.Content.Words.GetContent();

        return lockedWords.Content;
    }

    /// <summary>
    /// Returns the thumbnail of the specified 1-based page number as a PNG file,
    /// or <see langword="null"/> if it has not been produced yet.
    /// </summary>
    internal byte[]? GetThumbnail(int pageNumber)
    {
        using LockedContent<byte[]> lockedThumbnail = _pages[pageNumber - 1].CacheEntry.Thumbnail.GetContent();

        return lockedThumbnail.Content;
    }

    /// <summary>
    /// Returns the extracted content items of the specified 1-based page number in drawing order,
    /// or <see langword="null"/> if they have not been extracted yet.
    /// </summary>
    internal PdfPanelContentItem[]? GetContent(int pageNumber)
    {
        using LockedContent<PdfPanelContentItem[]> lockedContentItems = _pages[pageNumber - 1].CacheEntry.Content.ContentItems.GetContent();

        return lockedContentItems.Content;
    }

    /// <summary>
    /// Returns the information about the rendered content of the specified 1-based page number,
    /// or <see langword="null"/> if it has not been rendered yet.
    /// </summary>
    internal PdfPanelRenderedLayerInfo? GetRenderedContentInfo(int pageNumber) => _pages[pageNumber - 1].CacheEntry.Content.GetRenderedLayerInfo();

    /// <summary>
    /// Returns the information about the rendered annotations of the specified 1-based page number,
    /// or <see langword="null"/> if they have not been rendered yet.
    /// </summary>
    internal PdfPanelRenderedLayerInfo? GetRenderedAnnotationsInfo(int pageNumber) => _pages[pageNumber - 1].CacheEntry.AnnotationContent.GetRenderedLayerInfo();

    /// <summary>
    /// Returns <see langword="true"/> when the content picture of the specified 1-based page number
    /// must be regenerated for <paramref name="request"/>.
    /// </summary>
    internal bool NeedsContentUpdate(int pageNumber, PagesDrawingRequest request) => _pages[pageNumber - 1].CacheEntry.Content.NeedsPictureUpdate(request);

    /// <summary>
    /// Returns <see langword="true"/> when the annotation recording of the specified 1-based page number
    /// must be regenerated for <paramref name="request"/>.
    /// </summary>
    internal bool NeedsAnnotationUpdate(int pageNumber, PagesDrawingRequest request)
    {
        PdfPanelPage page = _pages[pageNumber - 1];

        return page.CacheEntry.GetAnnotations(page).Length > 0
            && page.CacheEntry.AnnotationContent.NeedsAnnotationRecordingUpdate(request);
    }

    /// <summary>
    /// Raises <see cref="PageUpdated"/> on the synchronization context.
    /// </summary>
    internal void NotifyPageUpdated(PageUpdatedArgs args) => Post(() => PageUpdated?.Invoke(this, args));

    /// <summary>
    /// Raises <see cref="PageTextExtracted"/> for the specified 1-based page number on the synchronization context.
    /// </summary>
    internal void NotifyPageTextExtracted(int pageNumber) => Post(() => PageTextExtracted?.Invoke(this, new PageTextExtractedEventArgs(pageNumber)));

    /// <summary>
    /// Raises <see cref="PageThumbnailRendered"/> on the synchronization context.
    /// </summary>
    internal void NotifyPageThumbnailRendered(PageThumbnailRenderedEventArgs args) => Post(() => PageThumbnailRendered?.Invoke(this, args));

    /// <summary>
    /// Raises <see cref="ExtractedContentCleared"/> for <paramref name="contentTypes"/> on the synchronization context.
    /// </summary>
    internal void NotifyExtractedContentCleared(CachedContentType contentTypes)
        => Post(() => ExtractedContentCleared?.Invoke(this, new ExtractedContentClearedEventArgs(contentTypes)));

    private void Post(Action raise)
    {
        if (SynchronizationContext == null)
        {
            raise();
            return;
        }

        SynchronizationContext.Post(_ => raise(), null);
    }

    /// <summary>
    /// Generates <see cref="PdfPanelDocument"/> from PDF document.
    /// </summary>
    /// <param name="document">PDF document.</param>
    /// <param name="loggerFactory">Logger factory instance.</param>
    /// <param name="synchronizationContext">
    /// Synchronization context page content notifications are posted to, or <see langword="null"/> to raise them on the decoding thread.
    /// </param>
    /// <returns><see cref="PdfPanelDocument"/>.</returns>
    public static PdfPanelDocument FromDocument(IPdfDocument document, ILoggerFactory loggerFactory, SynchronizationContext? synchronizationContext)
        => FromDocument(document, new SequentialWorkQueue(loggerFactory.CreateLogger<SequentialWorkQueue>()), new PdfNonYieldingObserverFactory(), synchronizationContext);

    /// <summary>
    /// Generates <see cref="PdfPanelDocument"/> from PDF document.
    /// </summary>
    /// <param name="document">PDF document.</param>
    /// <param name="processingQueue">Queue the page content is decoded on.</param>
    /// <param name="observerFactory">Factory of the execution observers the page content is decoded with.</param>
    /// <param name="synchronizationContext">
    /// Synchronization context page content notifications are posted to, or <see langword="null"/> to raise them on the decoding thread.
    /// </param>
    /// <returns><see cref="PdfPanelDocument"/>.</returns>
    public static PdfPanelDocument FromDocument(
        IPdfDocument document,
        IWorkQueue processingQueue,
        IPdfExecutionObserverFactory observerFactory,
        SynchronizationContext? synchronizationContext)
    {
        return new(document, processingQueue, observerFactory, synchronizationContext);
    }

    /// <summary>
    /// Disposes the underlying scheduler and releases all page resources.
    /// </summary>
    public void Dispose()
    {
        lock (_disposeLocker)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            Scheduler.Dispose();

            foreach (PdfPanelPage page in _pages)
            {
                page.CacheEntry.Dispose();
            }
        }
    }
}
