using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.PdfPanel.WorkQueue;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;


public interface IPdfPageContentProvider : IDisposable
{
    int GetPagesCount();

    void RefreshCache(IEnumerable<int> pagesToStore);

    ContentLocker<SKPicture> GetExistingContent(int pageNumber);

    ContentLocker<SKPicture> GetExistingAnnotationContent(int pageNumber);

    void UpdateContent(ContentProviderRequest request);

    PdfPanelPageInfo GetPageInfo(int pageNumber);
}

public class PdfPageUpdateCacheWorkItem : IWorkItem
{
    private readonly object _documentLocker;
    private readonly PdfDocument _document;
    private readonly ContentProviderRequest _request;

    public PdfPageUpdateCacheWorkItem(PdfPageCacheEntry cacheEntry, PdfDocument document, object documentLocker, ContentProviderRequest request)
    {
        CacheEntry = cacheEntry;
        _documentLocker = documentLocker;
        _document = document;
        _request = request;

    }

    public bool IsSkippable => false;

    public PdfPageCacheEntry CacheEntry { get; }

    public CancellationTokenSource CancellationTokenSource => _request.CancellationTokenSource;

    public void Process()
    {
        lock (_documentLocker)
        {
            if (!CacheEntry.Content.ContentCommandRecording.HasContent)
            {
                var recording = PdfDocumentContentExtensions.GeneratePageCommandRecording(_document, CacheEntry.PageNumber, CancellationTokenSource.Token);
                CacheEntry.Content.UpdateContentCommandRecording(recording);
            }
        }

        bool updated = false;

        if (!CacheEntry.Content.ContentPicture.HasContent || (CacheEntry.Content.IsScaleDependant && CacheEntry.Content.Scale != _request.RenderingParameters.ScaleFactor))
        {
            using var contentRecording = CacheEntry.Content.ContentCommandRecording.GetContent();

            if (contentRecording.HasContent)
            {
                var executionContext = new PdfCommandExecutionContext(_request.RenderingParameters, CancellationTokenSource.Token);
                var contentPicture = PdfDocumentContentExtensions.RecordingToSkPicture(CacheEntry.PageInfo, contentRecording.Content, executionContext);
                CacheEntry.Content.UpdateContentPicture(contentPicture, _request.RenderingParameters.ScaleFactor ?? 1);

                updated = true;
            }
        }

        if (updated)
        {
            _request.OnPageContentUpdated?.Invoke(CacheEntry.PageNumber, CacheEntry.Content.ContentPicture);
        }

        PdfAnnotationBase pageActiveAnnotation = null;
        PdfPanelPointerState pointerState = PdfPanelPointerState.None;

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

public class ContentProviderRequest
{
    public int PageNumber { get; set; }

    public CancellationTokenSource CancellationTokenSource { get; set; }

    public PdfRenderingParameters RenderingParameters { get; set; }

    public Action<int, ContentLocker<SKPicture>> OnPageContentUpdated { get; set; }

    public Action<int, ContentLocker<SKPicture>> OnPageAnnotationContentUpdated { get; set; }
}

public sealed class PdfPageContentProvider : IPdfPageContentProvider
{
    private readonly PdfDocument _document;
    private readonly IWorkQueue<PdfPageUpdateCacheWorkItem> _processingQueue;
    private readonly object _documentLocker = new object();
    private readonly PdfPageCacheEntry[] _cache;

    public PdfPageContentProvider(PdfDocument document, IWorkQueue<PdfPageUpdateCacheWorkItem> processingQueue)
    {
        _document = document;
        _cache = new PdfPageCacheEntry[document.Pages.Count];

        for (int i = 0; i < document.Pages.Count; i++)
        {
            _cache[i] = new PdfPageCacheEntry(i + 1, GetPageInfo(i + 1));
        }

        _processingQueue = processingQueue;
    }

    public int GetPagesCount()
    {
        return _cache.Length;
    }

    public void RefreshCache(IEnumerable<int> pagesToStore)
    {
        var pagesToStoreSet = new HashSet<int>(pagesToStore ?? []);

        foreach (var cacheEntry in _cache)
        {
            if (!pagesToStoreSet.Contains(cacheEntry.PageNumber))
            {
                cacheEntry.Clear();
            }
        }
    }

    public ContentLocker<SKPicture> GetExistingContent(int pageNumber)
    {
        var cacheEntry = _cache[pageNumber - 1];
        return cacheEntry.Content.ContentPicture;
    }

    public ContentLocker<SKPicture> GetExistingAnnotationContent(int pageNumber)
    {
        var cacheEntry = _cache[pageNumber - 1];
        return cacheEntry.AnnotationContent.ContentPicture;
    }

    public void UpdateContent(ContentProviderRequest request)
    {
        var cacheEntry = _cache[request.PageNumber - 1];
        var workItem = new PdfPageUpdateCacheWorkItem(cacheEntry, _document, _documentLocker, request);
        _processingQueue.Enqueue(workItem);
    }

    public PdfPanelPageInfo GetPageInfo(int pageNumber)
    {
        lock (_documentLocker)
        {
            return PdfDocumentContentExtensions.GetPageInfo(_document, pageNumber);
        }
    }

    public void Dispose()
    {
        _processingQueue.Dispose();

        foreach (var cacheEntry in _cache)
        {
            cacheEntry.Dispose();
        }
    }
}