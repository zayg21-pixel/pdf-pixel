using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.WorkQueue;
using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Work item that decodes a page's content and annotation pictures and stores them in <see cref="CacheEntry"/>.
/// Invokes <see cref="IPdfPageContentProvider.OnPageUpdated"/> when finished.
/// </summary>
public class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private readonly object _documentLocker = new();
    private readonly IPdfDocument _document;
    private readonly PagesDrawingRequest _request;
    private readonly Action<PageUpdatedArgs>? _onPageUpdated;
    private readonly IPdfCancellableExecutionObserver? _parseObserver;
    private readonly IPdfCancellableExecutionObserver? _contentObserver;

    /// <summary>
    /// Initializes the work item for the given cache entry and rendering request.
    /// Snapshots the current observers from the cache entry so replacements made by
    /// subsequent <see cref="PdfPageCacheEntry.InitializeForRendering"/> calls on the
    /// UI thread cannot affect this already-enqueued item.
    /// </summary>
    public PdfPageUpdateCacheWorkItem(PdfPageCacheEntry cacheEntry, IPdfDocument document, object documentLocker, PagesDrawingRequest request, Action<PageUpdatedArgs>? onPageUpdated)
    {
        if (cacheEntry == null)
        {
            throw new ArgumentNullException(nameof(cacheEntry));
        }

        CacheEntry = cacheEntry;
        _documentLocker = documentLocker;
        _document = document;
        _request = request;
        _onPageUpdated = onPageUpdated;
        _parseObserver = cacheEntry.ParseObserver;
        _contentObserver = cacheEntry.ContentObserver;
    }

    /// <inheritdoc />
    public bool IsSkippable => false;

    /// <summary>
    /// The cache entry this work item will populate.
    /// </summary>
    public PdfPageCacheEntry CacheEntry { get; }

    /// <inheritdoc />
    public void Process()
    {
        if (_parseObserver == null ||  _contentObserver == null)
        {
            return;
        }

        var contentUpdated = false;

        lock (_documentLocker)
        {
            if (!CacheEntry.Content.ContentCommandRecording.HasContent)
            {
                PdfCommandRecorder recording = _document.GeneratePageCommandRecording(CacheEntry.PageNumber, _parseObserver);
                CacheEntry.Content.UpdateContentCommandRecording(recording);
            }
        }

        if (CacheEntry.Content.NeedsUpdate(_request))
        {
            using LockedContent<PdfCommandRecorder> contentRecording = CacheEntry.Content.ContentCommandRecording.GetContent();

            if (contentRecording.Content != null)
            {
                using PdfCommandExecutionContext executionContext = new(_request.RenderingParameters, _documentLocker, _contentObserver);
                SkiaSharp.SKPicture? contentPicture = PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecording.Content, executionContext);
                CacheEntry.Content.UpdateContentPicture(contentPicture, _request);
                contentUpdated = true;
            }
        }

        if (contentUpdated)
        {
            _onPageUpdated?.Invoke(new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Content));
        }

        var annotationRecordingUpdated = false;

        lock (_documentLocker)
        {
            if (CacheEntry.Annotations?.Length > 0
                && (!CacheEntry.AnnotationContent.ContentCommandRecording.HasContent
                    || CacheEntry.AnnotationContent.LastRequest == null
                    || CacheEntry.AnnotationContent.LastRequest.ActiveAnnotation != _request.ActiveAnnotation
                    || CacheEntry.AnnotationContent.LastRequest.ActiveAnnotationState != _request.ActiveAnnotationState))
            {
                PdfCommandRecorder? annotationRecording = _document.GetAnnotationRecording(
                    CacheEntry.PageNumber,
                    _request.ActiveAnnotation?.PageAnnotation,
                    _request.ActiveAnnotationState,
                    _contentObserver);
                CacheEntry.AnnotationContent.UpdateContentCommandRecording(annotationRecording);
                annotationRecordingUpdated = true;
            }// TODO: [MEDIUM] explore capabilities for partial update of annotation recording
        }

        if (CacheEntry.AnnotationContent.ContentCommandRecording.HasContent
            && (annotationRecordingUpdated || CacheEntry.AnnotationContent.NeedsUpdate(_request)))
        {
            using LockedContent<PdfCommandRecorder> contentRecording = CacheEntry.AnnotationContent.ContentCommandRecording.GetContent();

            using PdfCommandExecutionContext executionContext = new(_request.RenderingParameters, _documentLocker, _contentObserver);
            SkiaSharp.SKPicture? contentPicture = PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecording.Content, executionContext);
            CacheEntry.AnnotationContent.UpdateContentPicture(contentPicture, _request);

            _onPageUpdated?.Invoke(new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Annotations));
        }
    }
}
