using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.PdfPanel.WorkQueue;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

public class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private readonly SemaphoreSlim _documentLocker;
    private readonly PdfDocument _document;
    private readonly UpdateContentRequest _request;
    private readonly Action<PageUpdatedArgs> _onPageUpdated;
    private readonly TokenSnapshot _tokenSnapshot;

    public PdfPageUpdateCacheWorkItem(PdfPageCacheEntry cacheEntry, PdfDocument document, SemaphoreSlim documentLocker, UpdateContentRequest request, Action<PageUpdatedArgs> onPageUpdated)
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

    public async Task ProcessAsync()
    {
        bool updated = false;

        await Task.Yield();

        var scaleFactor = _request.RenderingParameters.ScaleFactor ?? 1;

        await _documentLocker.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!CacheEntry.Content.ContentCommandRecording.HasContent)
            {
                var recording = PdfDocumentContentExtensions.GeneratePageCommandRecording(_document, CacheEntry.PageNumber, _tokenSnapshot.ParseToken);
                CacheEntry.Content.UpdateContentCommandRecording(recording);
            }
        }
        finally
        {
            _documentLocker.Release();
        }


        await _documentLocker.WaitAsync().ConfigureAwait(false);

        try
        {
            // TODO: [MEDIUM] this can be moved out of locker if DrawImageCommand would not have access to PDF resource stream
            if (!CacheEntry.Content.ContentPicture.HasContent || (CacheEntry.Content.IsScaleDependant && CacheEntry.Content.Scale != scaleFactor))
            {
                var contentRecording = CacheEntry.Content.ContentCommandRecording.GetContent();
                var contentRecordingScoped = contentRecording.Content;
                contentRecording.Dispose(); // we can't pass recording itself to another thread, but we can pass its content, since content is only updated here, we don't need extra read lock

                if (contentRecording.HasContent)
                {
                    var executionContext = new PdfCommandExecutionContext(_request.RenderingParameters, _tokenSnapshot.ContentToken);
                    var contentPicture = await PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecordingScoped, executionContext);
                    CacheEntry.Content.UpdateContentPicture(contentPicture, scaleFactor);

                    updated = true;
                }
            }
        }
        finally
        {
            _documentLocker.Release();
        }

        await Task.Yield();

        if (updated)
        {
            _onPageUpdated?.Invoke(new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Content));
            updated = false;
        }

        await _documentLocker.WaitAsync().ConfigureAwait(false);

        bool annotationRecordingUpdated = false;

        try
        {
            if (CacheEntry.Annotations?.Length > 0 && (!CacheEntry.AnnotationContent.ContentCommandRecording.HasContent || CacheEntry.ActiveAnnotation != _request.ActiveAnnotation || CacheEntry.CurrentPointerState != _request.PointerState))
            {
                var annotationRecording = PdfDocumentContentExtensions.GetAnnotationRecording(_document, CacheEntry.PageNumber, scaleFactor, _request.ActiveAnnotation?.Annotation, _request.PointerState, _tokenSnapshot.ContentToken);
                CacheEntry.AnnotationContent.UpdateContentCommandRecording(annotationRecording);
                CacheEntry.UpdateActiveAnnotationState(_request.ActiveAnnotation, _request.PointerState);
                annotationRecordingUpdated = true;
            }// TODO: [MEDIUM] explore capabilities for partial update of annotation recording
        }
        finally
        {
            _documentLocker.Release();
        }

        await Task.Yield();

        await _documentLocker.WaitAsync().ConfigureAwait(false);

        try
        {
            if (CacheEntry.AnnotationContent.ContentCommandRecording.HasContent && (annotationRecordingUpdated || !CacheEntry.AnnotationContent.ContentPicture.HasContent || (CacheEntry.AnnotationContent.IsScaleDependant && CacheEntry.AnnotationContent.Scale != scaleFactor)))
            {
                var contentRecording = CacheEntry.AnnotationContent.ContentCommandRecording.GetContent();
                var contentRecordingScoped = contentRecording.Content;
                contentRecording.Dispose(); // we can't pass recording itself to another thread, but we can pass its content, since content is only updated here, we don't need extra read lock

                var executionContext = new PdfCommandExecutionContext(_request.RenderingParameters, _tokenSnapshot.ContentToken);
                var contentPicture = await PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecordingScoped, executionContext);
                CacheEntry.AnnotationContent.UpdateContentPicture(contentPicture, scaleFactor);

                updated = true;
            }
        }
        finally
        {
            _documentLocker.Release();
        }

        await Task.Yield();

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
