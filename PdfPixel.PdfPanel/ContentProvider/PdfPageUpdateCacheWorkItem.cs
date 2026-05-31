using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Work item that decodes a page's content and annotation pictures and stores them in <see cref="CacheEntry"/>.
/// Invokes <see cref="IPdfPageContentProvider.OnPageUpdated"/> when finished.
/// </summary>
public class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private readonly object _documentLocker = new();
    private readonly IPdfDocument _document;
    private readonly UpdateContentRequest _request;
    private readonly Action<PageUpdatedArgs>? _onPageUpdated;
    private readonly TokenSnapshot _tokenSnapshot;

    /// <summary>
    /// Initializes the work item for the given cache entry and rendering request.
    /// </summary>
    public PdfPageUpdateCacheWorkItem(PdfPageCacheEntry cacheEntry, IPdfDocument document, object documentLocker, UpdateContentRequest request, Action<PageUpdatedArgs>? onPageUpdated)
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

        _tokenSnapshot = new TokenSnapshot
        {
            ParseToken = cacheEntry.ParseCancellationTokenSource?.Token ?? CancellationToken.None,
            ContentToken = cacheEntry.ContentCancellationTokenSource?.Token ?? CancellationToken.None
        };
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
        var updated = false;

        float scaleFactor = _request.RenderingParameters.ScaleFactor ?? 1;

        lock (_documentLocker)
        {
            if (!CacheEntry.Content.ContentCommandRecording.HasContent)
            {
                PdfCancellationExecutionObserver observer = new(_tokenSnapshot.ParseToken);
                PdfCommandRecorder recording = _document.GeneratePageCommandRecording(CacheEntry.PageNumber, observer);
                CacheEntry.Content.UpdateContentCommandRecording(recording);
            }
        }

        if (!CacheEntry.Content.ContentPicture.HasContent || (CacheEntry.Content.IsScaleDependant && CacheEntry.Content.Scale != scaleFactor))
        {
            using LockedContent<PdfCommandRecorder> contentRecording = CacheEntry.Content.ContentCommandRecording.GetContent();

            if (contentRecording.Content != null)
            {
                PdfCancellationExecutionObserver observer = new(_tokenSnapshot.ContentToken);
                PdfCommandExecutionContext executionContext = new(_request.RenderingParameters, _documentLocker, observer);
                SkiaSharp.SKPicture? contentPicture = PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecording.Content, executionContext);
                CacheEntry.Content.UpdateContentPicture(contentPicture, scaleFactor);
                updated = true;
            }
        }

        var annotationRecordingUpdated = false;

        lock (_documentLocker)
        {
            if (CacheEntry.Annotations?.Length > 0
                && (!CacheEntry.AnnotationContent.ContentCommandRecording.HasContent
                    || CacheEntry.ActiveAnnotation != _request.ActiveAnnotation
                    || CacheEntry.CurrentPointerState != _request.PointerState))
            {
                PdfCancellationExecutionObserver observer = new(_tokenSnapshot.ContentToken);
                PdfCommandRecorder? annotationRecording = _document.GetAnnotationRecording(CacheEntry.PageNumber, _request.ActiveAnnotation?.PageAnnotation, _request.PointerState, observer);
                CacheEntry.AnnotationContent.UpdateContentCommandRecording(annotationRecording);
                CacheEntry.UpdateActiveAnnotationState(_request.ActiveAnnotation, _request.PointerState);
                annotationRecordingUpdated = true;
            }// TODO: [MEDIUM] explore capabilities for partial update of annotation recording
        }

        if (CacheEntry.AnnotationContent.ContentCommandRecording.HasContent
            && (annotationRecordingUpdated
                || !CacheEntry.AnnotationContent.ContentPicture.HasContent
                || (CacheEntry.AnnotationContent.IsScaleDependant && CacheEntry.AnnotationContent.Scale != scaleFactor)))
        {
            using LockedContent<PdfCommandRecorder> contentRecording = CacheEntry.AnnotationContent.ContentCommandRecording.GetContent();

            PdfCancellationExecutionObserver observer = new(_tokenSnapshot.ContentToken);
            PdfCommandExecutionContext executionContext = new(_request.RenderingParameters, _documentLocker, observer);
            SkiaSharp.SKPicture? contentPicture = PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecording.Content, executionContext);
            CacheEntry.AnnotationContent.UpdateContentPicture(contentPicture, scaleFactor);

            updated = true;
        }

        if (updated)
        {
            _onPageUpdated?.Invoke(new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Annotations));
        }
    }

    private struct TokenSnapshot
    {
        public CancellationToken ParseToken;
        public CancellationToken ContentToken;
    }
}
