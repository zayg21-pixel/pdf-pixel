using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

public class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private readonly object _documentLocker = new object();
    private readonly IPdfDocument _document;
    private readonly UpdateContentRequest _request;
    private readonly Action<PageUpdatedArgs> _onPageUpdated;
    private readonly TokenSnapshot _tokenSnapshot;

    public PdfPageUpdateCacheWorkItem(PdfPageCacheEntry cacheEntry, IPdfDocument document, object documentLocker, UpdateContentRequest request, Action<PageUpdatedArgs> onPageUpdated)
    {
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

    public bool IsSkippable => false;

    public PdfPageCacheEntry CacheEntry { get; }

    public void Process()
    {
        bool updated = false;

        var scaleFactor = _request.RenderingParameters.ScaleFactor ?? 1;

        lock (_documentLocker)
        {
            if (!CacheEntry.Content.ContentCommandRecording.HasContent)
            {
                var observer = new PdfCancellationExecutionObserver(_tokenSnapshot.ParseToken);
                var recording = PdfDocumentContentExtensions.GeneratePageCommandRecording(_document, CacheEntry.PageNumber, observer);
                CacheEntry.Content.UpdateContentCommandRecording(recording);
            }
        }

        if (!CacheEntry.Content.ContentPicture.HasContent || (CacheEntry.Content.IsScaleDependant && CacheEntry.Content.Scale != scaleFactor))
        {
            using var contentRecording = CacheEntry.Content.ContentCommandRecording.GetContent();

            if (contentRecording.HasContent)
            {
                var observer = new PdfCancellationExecutionObserver(_tokenSnapshot.ContentToken);
                var executionContext = new PdfCommandExecutionContext(_request.RenderingParameters, _documentLocker, observer);
                var contentPicture = PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecording.Content, executionContext);
                CacheEntry.Content.UpdateContentPicture(contentPicture, scaleFactor);
                updated = true;
            }
        }

        bool annotationRecordingUpdated = false;

        lock (_documentLocker)
        {
            if (CacheEntry.Annotations?.Length > 0 && (!CacheEntry.AnnotationContent.ContentCommandRecording.HasContent || CacheEntry.ActiveAnnotation != _request.ActiveAnnotation || CacheEntry.CurrentPointerState != _request.PointerState))
            {
                var observer = new PdfCancellationExecutionObserver(_tokenSnapshot.ContentToken);
                var annotationRecording = PdfDocumentContentExtensions.GetAnnotationRecording(_document, CacheEntry.PageNumber, _request.ActiveAnnotation?.Annotation, _request.PointerState, observer);
                CacheEntry.AnnotationContent.UpdateContentCommandRecording(annotationRecording);
                CacheEntry.UpdateActiveAnnotationState(_request.ActiveAnnotation, _request.PointerState);
                annotationRecordingUpdated = true;
            }// TODO: [MEDIUM] explore capabilities for partial update of annotation recording
        }

        if (CacheEntry.AnnotationContent.ContentCommandRecording.HasContent && (annotationRecordingUpdated || !CacheEntry.AnnotationContent.ContentPicture.HasContent || (CacheEntry.AnnotationContent.IsScaleDependant && CacheEntry.AnnotationContent.Scale != scaleFactor)))
        {
            using var contentRecording = CacheEntry.AnnotationContent.ContentCommandRecording.GetContent();

            var observer = new PdfCancellationExecutionObserver(_tokenSnapshot.ContentToken);
            var executionContext = new PdfCommandExecutionContext(_request.RenderingParameters, _documentLocker, observer);
            var contentPicture = PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecording.Content, executionContext);
            CacheEntry.AnnotationContent.UpdateContentPicture(contentPicture, scaleFactor);

            updated = true;
        }

        if (updated)
        {
            _onPageUpdated?.Invoke(new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Annotations));
            updated = false;
        }
    }

    private struct TokenSnapshot
    {
        public CancellationToken ParseToken;
        public CancellationToken ContentToken;
    }
}
