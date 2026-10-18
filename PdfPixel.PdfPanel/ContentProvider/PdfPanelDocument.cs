using PdfPixel.Content.Model;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Content;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Text;
using System;
using System.Collections.Generic;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Document of a panel and the content produced for its pages: page information, decoded pictures, words,
/// content items and thumbnails. Raises the notifications of produced and cleared content on the synchronization context.
/// </summary>
internal sealed class PdfPanelDocument : IDisposable
{
    private readonly PdfPageCacheEntry[] _entries;
    private IReadOnlyDictionary<PdfReference, bool>? _optionalContentStates;

    /// <summary>
    /// Initializes the storage for the pages of <paramref name="document"/>. Notifications are posted to
    /// <paramref name="synchronizationContext"/>, or raised on the calling thread when it is <see langword="null"/>.
    /// </summary>
    public PdfPanelDocument(IPdfDocument document, SynchronizationContext? synchronizationContext)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        SynchronizationContext = synchronizationContext;
        _entries = new PdfPageCacheEntry[document.Pages.Count];

        for (int index = 0; index < _entries.Length; index++)
        {
            _entries[index] = new PdfPageCacheEntry(index + 1, PdfDocumentContentExtensions.GetPageInfo(document, index + 1));
        }
    }

    /// <summary>
    /// Raised when a page's content or annotations have been decoded and are ready to render.
    /// </summary>
    public event EventHandler<PageUpdatedArgs>? PageUpdated;

    /// <summary>
    /// Raised when a page's characters have been extracted, by text extraction or by rendering.
    /// </summary>
    public event EventHandler<PageTextExtractedEventArgs>? PageTextExtracted;

    /// <summary>
    /// Raised when a requested thumbnail finished.
    /// </summary>
    public event EventHandler<PageThumbnailRenderedEventArgs>? PageThumbnailRendered;

    /// <summary>
    /// Raised when extracted content of every page was cleared.
    /// </summary>
    public event EventHandler<ExtractedContentClearedEventArgs>? ExtractedContentCleared;

    /// <summary>
    /// Raised on the setting thread when <see cref="OptionalContentStates"/> changed.
    /// </summary>
    public event EventHandler? OptionalContentStatesChanged;

    /// <summary>
    /// Document the pages are decoded from.
    /// </summary>
    public IPdfDocument Document { get; }

    /// <summary>
    /// Synchronisation object used to serialise access to <see cref="Document"/>.
    /// </summary>
    public object DocumentLocker { get; } = new();

    /// <summary>
    /// Synchronization context the notifications are posted to, or <see langword="null"/> when they are raised on the calling thread.
    /// </summary>
    public SynchronizationContext? SynchronizationContext { get; }

    /// <summary>
    /// Cache entries of the pages, in page order.
    /// </summary>
    public IReadOnlyList<PdfPageCacheEntry> Entries => _entries;

    /// <summary>
    /// Number of pages in the document.
    /// </summary>
    public int PageCount => _entries.Length;

    /// <summary>
    /// Optional content states the words and thumbnails are produced with.
    /// Setting different states raises <see cref="OptionalContentStatesChanged"/>.
    /// </summary>
    public IReadOnlyDictionary<PdfReference, bool>? OptionalContentStates
    {
        get => _optionalContentStates;

        set
        {
            // TODO: AreOptionalContentStatesEqual must not be public, we need separate comparer. Somewhere.
            if (PagesDrawingRequest.AreOptionalContentStatesEqual(_optionalContentStates, value))
            {
                return;
            }

            _optionalContentStates = value;
            OptionalContentStatesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Returns the <see cref="PdfPanelPageInfo"/> for the specified 1-based page number.
    /// </summary>
    public PdfPanelPageInfo GetPageInfo(int pageNumber) => _entries[pageNumber - 1].PageInfo;

    /// <summary>
    /// Returns the annotation popups for the specified 1-based page number.
    /// </summary>
    public PdfAnnotationPopup[] GetAnnotationPopups(int pageNumber) => _entries[pageNumber - 1].GetAnnotations(Document, DocumentLocker);

    /// <summary>
    /// Returns the currently cached <see cref="PdfContentPictures"/> for the specified 1-based page number.
    /// Returns empty pictures if the page has not been decoded yet.
    /// </summary>
    public PdfContentPictures GetContentPictures(int pageNumber) => _entries[pageNumber - 1].GetContentPictures();

    /// <summary>
    /// Returns the extracted words of the specified 1-based page number in reading order,
    /// or <see langword="null"/> if they have not been extracted yet.
    /// </summary>
    public PdfWord[]? GetWords(int pageNumber)
    {
        using LockedContent<PdfWord[]> lockedWords = _entries[pageNumber - 1].Content.Words.GetContent();

        return lockedWords.Content;
    }

    /// <summary>
    /// Returns the thumbnail of the specified 1-based page number as a PNG file,
    /// or <see langword="null"/> if it has not been produced yet.
    /// </summary>
    public byte[]? GetThumbnail(int pageNumber)
    {
        using LockedContent<byte[]> lockedThumbnail = _entries[pageNumber - 1].Thumbnail.GetContent();

        return lockedThumbnail.Content;
    }

    /// <summary>
    /// Returns the extracted content items of the specified 1-based page number in drawing order,
    /// or <see langword="null"/> if they have not been extracted yet.
    /// </summary>
    public PdfPanelContentItem[]? GetContent(int pageNumber)
    {
        using LockedContent<PdfPanelContentItem[]> lockedContentItems = _entries[pageNumber - 1].Content.ContentItems.GetContent();

        return lockedContentItems.Content;
    }

    /// <summary>
    /// Returns the information about the rendered content of the specified 1-based page number,
    /// or <see langword="null"/> if it has not been rendered yet.
    /// </summary>
    public PdfPanelRenderedLayerInfo? GetRenderedContentInfo(int pageNumber) => _entries[pageNumber - 1].Content.GetRenderedLayerInfo();

    /// <summary>
    /// Returns the information about the rendered annotations of the specified 1-based page number,
    /// or <see langword="null"/> if they have not been rendered yet.
    /// </summary>
    public PdfPanelRenderedLayerInfo? GetRenderedAnnotationsInfo(int pageNumber) => _entries[pageNumber - 1].AnnotationContent.GetRenderedLayerInfo();

    /// <summary>
    /// Returns <see langword="true"/> when the content picture of the specified 1-based page number
    /// must be regenerated for <paramref name="request"/>.
    /// </summary>
    public bool NeedsContentUpdate(int pageNumber, PagesDrawingRequest request) => _entries[pageNumber - 1].Content.NeedsPictureUpdate(request);

    /// <summary>
    /// Returns <see langword="true"/> when the annotation recording of the specified 1-based page number
    /// must be regenerated for <paramref name="request"/>.
    /// </summary>
    public bool NeedsAnnotationUpdate(int pageNumber, PagesDrawingRequest request)
    {
        PdfPageCacheEntry cacheEntry = _entries[pageNumber - 1];

        return cacheEntry.GetAnnotations(Document, DocumentLocker).Length > 0
            && cacheEntry.AnnotationContent.NeedsAnnotationRecordingUpdate(request);
    }

    /// <summary>
    /// Raises <see cref="PageUpdated"/> on the synchronization context.
    /// </summary>
    public void NotifyPageUpdated(PageUpdatedArgs args) => Post(() => PageUpdated?.Invoke(this, args));

    /// <summary>
    /// Raises <see cref="PageTextExtracted"/> for the specified 1-based page number on the synchronization context.
    /// </summary>
    public void NotifyPageTextExtracted(int pageNumber) => Post(() => PageTextExtracted?.Invoke(this, new PageTextExtractedEventArgs(pageNumber)));

    /// <summary>
    /// Raises <see cref="PageThumbnailRendered"/> on the synchronization context.
    /// </summary>
    public void NotifyPageThumbnailRendered(PageThumbnailRenderedEventArgs args) => Post(() => PageThumbnailRendered?.Invoke(this, args));

    /// <summary>
    /// Raises <see cref="ExtractedContentCleared"/> for <paramref name="contentTypes"/> on the synchronization context.
    /// </summary>
    public void NotifyExtractedContentCleared(CachedContentType contentTypes)
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

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (PdfPageCacheEntry cacheEntry in _entries)
        {
            cacheEntry.Dispose();
        }
    }
}
