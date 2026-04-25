using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.PdfPanel.WorkQueue;
using SkiaSharp;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

public class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private readonly SemaphoreSlim _documentLocker;
    private readonly PdfDocument _document;
    private readonly UpdateContentRequest _request;

    public PdfPageUpdateCacheWorkItem(PdfPageCacheEntry cacheEntry, PdfDocument document, SemaphoreSlim documentLocker, UpdateContentRequest request)
    {
        CacheEntry = cacheEntry;
        _documentLocker = documentLocker;
        _document = document;
        _request = request;

    }

    public bool IsSkippable => false;

    public PdfPageCacheEntry CacheEntry { get; }

    public CancellationTokenSource CancellationTokenSource => _request.CancellationTokenSource;

    public async Task ProcessAsync()
    {
        bool updated = false;

        await Task.Yield();

        await _documentLocker.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!CacheEntry.Content.ContentCommandRecording.HasContent)
            {
                var recording = PdfDocumentContentExtensions.GeneratePageCommandRecording(_document, CacheEntry.PageNumber, CancellationTokenSource.Token);
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
            if (!CacheEntry.Content.ContentPicture.HasContent || (CacheEntry.Content.IsScaleDependant && CacheEntry.Content.Scale != _request.RenderingParameters.ScaleFactor))
            {
                var contentRecording = CacheEntry.Content.ContentCommandRecording.GetContent();
                var contentRecordingScoped = contentRecording.Content;
                contentRecording.Dispose(); // we can't pass recording itself to another thread, but we can pass its content, since content is only updated here, we don't need extra read lock

                if (contentRecording.HasContent)
                {
                    var executionContext = new PdfCommandExecutionContext(_request.RenderingParameters, CancellationTokenSource.Token);
                    var contentPicture = await PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecordingScoped, executionContext);
                    CacheEntry.Content.UpdateContentPicture(contentPicture, _request.RenderingParameters.ScaleFactor ?? 1);

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
            _request.OnPageUpdated?.Invoke(CacheEntry.PageNumber, CacheEntry.Content.ContentPicture);
        }

        var annotations = CacheEntry.Annotations;

        if (annotations == null)
        {
            return;
        }

        PdfAnnotationBase pageActiveAnnotation = null;
        PdfPanelPointerState pointerState = PdfPanelPointerState.None;

        //var activeAnnotation = 

        //if (cachedPicture.HasAnnotations && activeAnnotationPopup != null && TryGetPage(pageNumber, out var panelPage))
        //{
        //    foreach (var popup in panelPage.Popups)
        //    {
        //        if (popup == activeAnnotationPopup)
        //        {
        //            pageActiveAnnotation = activeAnnotationPopup.Annotation;
        //            pointerState = activeAnnotationState;
        //            break;
        //        }
        //    }
        //}

        //bool annotationChanged = cachedPicture.ActiveAnnotation != pageActiveAnnotation;
        //bool stateChangedWithinAnnotation = cachedPicture.ActiveAnnotationState != pointerState && pageActiveAnnotation != null;

        //cachedPicture.ActiveAnnotationState = pointerState;
        //cachedPicture.ActiveAnnotation = pageActiveAnnotation;

        //if (annotationChanged || stateChangedWithinAnnotation)
        //{
        //    // TODO: [HIGH] we're leaving initial page without annotations
        //    cachedPicture.UpdateAnnotationRecording(null);
        //}

        //return cachedPicture;
    }
}
