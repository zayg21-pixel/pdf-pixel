using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.PdfPanel.WorkQueue;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

public class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private readonly object _documentLocker = new object();
    private readonly PdfDocument _document;
    private readonly UpdateContentRequest _request;
    private readonly Action<PageUpdatedArgs> _onPageUpdated;
    private readonly TokenSnapshot _tokenSnapshot;

    public PdfPageUpdateCacheWorkItem(PdfPageCacheEntry cacheEntry, PdfDocument document, object documentLocker, UpdateContentRequest request, Action<PageUpdatedArgs> onPageUpdated)
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
                var recording = PdfDocumentContentExtensions.GeneratePageCommandRecording(_document, CacheEntry.PageNumber, _tokenSnapshot.ParseToken);
                CacheEntry.Content.UpdateContentCommandRecording(recording);
            }
        }

        // TODO: [MEDIUM] this can be moved out of locker if DrawImageCommand would not have access to PDF resource stream
        if (!CacheEntry.Content.ContentPicture.HasContent || (CacheEntry.Content.IsScaleDependant && CacheEntry.Content.Scale != scaleFactor))
        {
            using var contentRecording = CacheEntry.Content.ContentCommandRecording.GetContent();

            if (contentRecording.HasContent)
            {
                var executionContext = new PdfCommandExecutionContext(_request.RenderingParameters, _tokenSnapshot.ContentToken);
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
                var annotationRecording = PdfDocumentContentExtensions.GetAnnotationRecording(_document, CacheEntry.PageNumber, scaleFactor, _request.ActiveAnnotation?.Annotation, _request.PointerState, _tokenSnapshot.ContentToken);
                CacheEntry.AnnotationContent.UpdateContentCommandRecording(annotationRecording);
                CacheEntry.UpdateActiveAnnotationState(_request.ActiveAnnotation, _request.PointerState);
                annotationRecordingUpdated = true;
            }// TODO: [MEDIUM] explore capabilities for partial update of annotation recording
        }


        lock (_documentLocker)
        {
            if (CacheEntry.AnnotationContent.ContentCommandRecording.HasContent && (annotationRecordingUpdated || !CacheEntry.AnnotationContent.ContentPicture.HasContent || (CacheEntry.AnnotationContent.IsScaleDependant && CacheEntry.AnnotationContent.Scale != scaleFactor)))
            {
                using var contentRecording = CacheEntry.AnnotationContent.ContentCommandRecording.GetContent();

                var executionContext = new PdfCommandExecutionContext(_request.RenderingParameters, _tokenSnapshot.ContentToken);
                var contentPicture = PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecording.Content, executionContext);
                CacheEntry.AnnotationContent.UpdateContentPicture(contentPicture, scaleFactor);

                updated = true;
            }
        }

        if (updated)
        {
            _onPageUpdated?.Invoke(new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Annotations));
            updated = false;
        }
    }

    private void RecordWithEarlyFlush(PdfCommandRecorder commandRecording, PdfCommandExecutionContext executionContext, float scaleFactor)
    {
        // TODO: [HIGH] cleanup, make universal
        const int FlushThresholdMs = 300;

        var bounds = SKRect.Create(CacheEntry.PageInfo.Width, CacheEntry.PageInfo.Height);
        using var recorder = new SKPictureRecorder();

        int executedCount = 0;
        int skipCount = 0;
        var sw = new Stopwatch();

        executionContext.OnCommandExecuted = () =>
        {
            executedCount++;
            if (executedCount <= skipCount) return;
            if (!sw.IsRunning) sw.Restart();
            if (sw.ElapsedMilliseconds < FlushThresholdMs) return;
            throw new TimeoutException();
        };

        var canvas = recorder.BeginRecording(bounds);
        while (true)
        {
            try
            {
                lock (_documentLocker)
                {
                    commandRecording.Replay(canvas, [], executionContext);
                }

                break;
            }
            catch (TimeoutException)
            {
                canvas.Flush();
                var snapshot = recorder.EndRecording();
                CacheEntry.Content.UpdateContentPicture(snapshot, scaleFactor);
                _onPageUpdated?.Invoke(new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Content));

                skipCount = executedCount;
                executedCount = 0;
                sw.Reset();
                canvas = recorder.BeginRecording(bounds);
            }
        }

        canvas.Flush();
        CacheEntry.Content.UpdateContentPicture(recorder.EndRecording(), scaleFactor);
        executionContext.OnCommandExecuted = null;

        _onPageUpdated?.Invoke(new PageUpdatedArgs(CacheEntry.PageNumber, CacheEntry.GetContentPictures(), UpdatedContentType.Content));
    }


    private struct TokenSnapshot
    {
        public CancellationToken ParseToken;
        public CancellationToken ContentToken;
    }
}
